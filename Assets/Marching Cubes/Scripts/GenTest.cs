using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GenTest : MonoBehaviour
{
	const string ChunkNamePrefix = "Chunk (";

	[Header("Init Settings")]
	public int numChunks = 10;

	public int numPointsPerAxis = 35;
	public float boundsSize = 500f;
	public float isoLevel = 0f;
	public bool useFlatShading;

	public float noiseScale = 0.75f;
	public float noiseHeightMultiplier = 0.075f;
	public bool blurMap = true;
	public int blurRadius = 3;

	[Header("References")]
	public ComputeShader meshCompute;
	public ComputeShader densityCompute;
	public ComputeShader blurCompute;
	public Material material;


	// Private
	ComputeBuffer triangleBuffer;
	ComputeBuffer triCountBuffer;
	[HideInInspector] public RenderTexture rawDensityTexture;
	[HideInInspector] public RenderTexture processedDensityTexture;
	Chunk[] chunks;

	VertexData[] vertexDataArray;

	int totalVerts;

	// Stopwatches
	System.Diagnostics.Stopwatch timer_fetchVertexData;
	System.Diagnostics.Stopwatch timer_processVertexData;

	void Start()
	{
		RebuildPlanet();
	}

	void Reset()
	{
		ApplySebastianSceneDefaults();
		AssignDefaultComputeShaders();
	}

	void OnValidate()
	{
		numChunks = Mathf.Max(1, numChunks);
		numPointsPerAxis = Mathf.Max(2, numPointsPerAxis);
		boundsSize = Mathf.Max(0.01f, boundsSize);
		blurRadius = Mathf.Max(1, blurRadius);
		AssignDefaultComputeShaders();
	}

	void InitTextures()
	{

		// Explanation of texture size:
		// Each pixel maps to one point.
		// Each chunk has "numPointsPerAxis" points along each axis
		// The last points of each chunk overlap in space with the first points of the next chunk
		// Therefore we need one fewer pixel than points for each added chunk
		int size = numChunks * (numPointsPerAxis - 1) + 1;
		Create3DTexture(ref rawDensityTexture, size, "Raw Density Texture");
		Create3DTexture(ref processedDensityTexture, size, "Processed Density Texture");

		if (!blurMap)
		{
			processedDensityTexture = rawDensityTexture;
		}

		// Set textures on compute shaders
		densityCompute.SetTexture(0, "DensityTexture", rawDensityTexture);
		blurCompute.SetTexture(0, "Source", rawDensityTexture);
		blurCompute.SetTexture(0, "Result", processedDensityTexture);
		meshCompute.SetTexture(0, "DensityTexture", (blurCompute) ? processedDensityTexture : rawDensityTexture);
	}

	[ContextMenu("Generate All Chunks")]
	public void GenerateAllChunks()
	{
		if (!HasRequiredComputeShaders())
		{
			return;
		}

		if (rawDensityTexture == null || triangleBuffer == null || chunks == null)
		{
			InitTextures();
			CreateBuffers();
			CreateChunks();
		}

		// Create timers:
		timer_fetchVertexData = new System.Diagnostics.Stopwatch();
		timer_processVertexData = new System.Diagnostics.Stopwatch();

		totalVerts = 0;
		ComputeDensity();


		for (int i = 0; i < chunks.Length; i++)
		{
			GenerateChunk(chunks[i]);
		}
		Debug.Log("Total verts " + totalVerts);

		// Print timers:
		Debug.Log("Fetch vertex data: " + timer_fetchVertexData.ElapsedMilliseconds + " ms");
		Debug.Log("Process vertex data: " + timer_processVertexData.ElapsedMilliseconds + " ms");
		Debug.Log("Sum: " + (timer_fetchVertexData.ElapsedMilliseconds + timer_processVertexData.ElapsedMilliseconds));


	}

	void ComputeDensity()
	{
		// Get points (each point is a vector4: xyz = position, w = density)
		int textureSize = rawDensityTexture.width;

		densityCompute.SetInt("textureSize", textureSize);

		densityCompute.SetFloat("planetSize", boundsSize);
		densityCompute.SetFloat("noiseHeightMultiplier", noiseHeightMultiplier);
		densityCompute.SetFloat("noiseScale", noiseScale);

		ComputeHelper.Dispatch(densityCompute, textureSize, textureSize, textureSize);

		ProcessDensityMap();
	}

	void ProcessDensityMap()
	{
		if (blurMap)
		{
			int size = rawDensityTexture.width;
			blurCompute.SetInts("brushCentre", 0, 0, 0);
			blurCompute.SetInt("blurRadius", blurRadius);
			blurCompute.SetInt("textureSize", rawDensityTexture.width);
			ComputeHelper.Dispatch(blurCompute, size, size, size);
		}
	}

	void GenerateChunk(Chunk chunk)
	{


		// Marching cubes
		int numVoxelsPerAxis = numPointsPerAxis - 1;
		int marchKernel = 0;


		meshCompute.SetInt("textureSize", processedDensityTexture.width);
		meshCompute.SetInt("numPointsPerAxis", numPointsPerAxis);
		meshCompute.SetFloat("isoLevel", isoLevel);
		meshCompute.SetFloat("planetSize", boundsSize);
		triangleBuffer.SetCounterValue(0);
		meshCompute.SetBuffer(marchKernel, "triangles", triangleBuffer);

		Vector3 chunkCoord = (Vector3)chunk.id * (numPointsPerAxis - 1);
		meshCompute.SetVector("chunkCoord", chunkCoord);

		ComputeHelper.Dispatch(meshCompute, numVoxelsPerAxis, numVoxelsPerAxis, numVoxelsPerAxis, marchKernel);

		// Create mesh
		int[] vertexCountData = new int[1];
		triCountBuffer.SetData(vertexCountData);
		ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);

		timer_fetchVertexData.Start();
		triCountBuffer.GetData(vertexCountData);

		int numVertices = vertexCountData[0] * 3;

		// Fetch vertex data from GPU

		triangleBuffer.GetData(vertexDataArray, 0, 0, numVertices);

		timer_fetchVertexData.Stop();

		//CreateMesh(vertices);
		timer_processVertexData.Start();
		chunk.CreateMesh(vertexDataArray, numVertices, useFlatShading);
		timer_processVertexData.Stop();
	}

	void Update()
	{
		if (material != null && processedDensityTexture != null)
		{
			material.SetTexture("DensityTex", processedDensityTexture);
			material.SetFloat("planetBoundsSize", boundsSize);
		}

		/*
		if (Input.GetKeyDown(KeyCode.G))
		{
			Debug.Log("Generate");
			GenerateAllChunks();
		}
		*/
	}



	void CreateBuffers()
	{
		int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
		int numVoxelsPerAxis = numPointsPerAxis - 1;
		int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
		int maxTriangleCount = numVoxels * 5;
		int maxVertexCount = maxTriangleCount * 3;

		triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
		triangleBuffer = new ComputeBuffer(maxVertexCount, ComputeHelper.GetStride<VertexData>(), ComputeBufferType.Append);
		vertexDataArray = new VertexData[maxVertexCount];
	}

	void ReleaseBuffers()
	{
		ComputeHelper.Release(triangleBuffer, triCountBuffer);
	}

	void OnDestroy()
	{
		ReleaseBuffers();
		if (chunks == null)
		{
			return;
		}

		foreach (Chunk chunk in chunks)
		{
			if (chunk != null)
			{
				chunk.Release();
			}
		}
	}

	[ContextMenu("Rebuild Planet")]
	public void RebuildPlanet()
	{
		AssignDefaultComputeShaders();
		if (!HasRequiredComputeShaders())
		{
			return;
		}

		ClearGeneratedChunks();
		ReleaseBuffers();
		InitTextures();
		CreateBuffers();
		CreateChunks();

		var sw = System.Diagnostics.Stopwatch.StartNew();
		GenerateAllChunks();
		Debug.Log("Generation Time: " + sw.ElapsedMilliseconds + " ms");
	}

	[ContextMenu("Clear Generated Chunks")]
	public void ClearGeneratedChunks()
	{
		if (chunks != null)
		{
			for (int i = 0; i < chunks.Length; i++)
			{
				Chunk chunk = chunks[i];
				if (chunk == null)
				{
					continue;
				}

				chunk.Release();
			}
		}

		chunks = null;
		ClearChunkChildren();
	}

	[ContextMenu("Apply Sebastian Scene Defaults")]
	public void ApplySebastianSceneDefaults()
	{
		numChunks = 10;
		numPointsPerAxis = 35;
		boundsSize = 500f;
		isoLevel = 0f;
		useFlatShading = false;
		noiseScale = 0.75f;
		noiseHeightMultiplier = 0.075f;
		blurMap = true;
		blurRadius = 3;
		AssignDefaultComputeShaders();
	}

	[ContextMenu("Apply Quest Friendly Defaults")]
	public void ApplyQuestFriendlyDefaults()
	{
		numChunks = 4;
		numPointsPerAxis = 18;
		boundsSize = 500f;
		isoLevel = 0f;
		useFlatShading = false;
		noiseScale = 0.75f;
		noiseHeightMultiplier = 0.075f;
		blurMap = true;
		blurRadius = 2;
		AssignDefaultComputeShaders();
	}

	void ClearChunkChildren()
	{
		for (int i = transform.childCount - 1; i >= 0; i--)
		{
			Transform child = transform.GetChild(i);
			if (child == null || !child.name.StartsWith(ChunkNamePrefix))
			{
				continue;
			}

			if (Application.isPlaying)
			{
				Destroy(child.gameObject);
			}
			else
			{
				DestroyImmediate(child.gameObject);
			}
		}
	}


	void CreateChunks()
	{
		chunks = new Chunk[numChunks * numChunks * numChunks];
		float chunkSize = (boundsSize) / numChunks;
		int i = 0;

		for (int y = 0; y < numChunks; y++)
		{
			for (int x = 0; x < numChunks; x++)
			{
				for (int z = 0; z < numChunks; z++)
				{
					Vector3Int coord = new Vector3Int(x, y, z);
					float posX = (-(numChunks - 1f) / 2 + x) * chunkSize;
					float posY = (-(numChunks - 1f) / 2 + y) * chunkSize;
					float posZ = (-(numChunks - 1f) / 2 + z) * chunkSize;
					Vector3 centre = new Vector3(posX, posY, posZ);

					GameObject meshHolder = new GameObject($"{ChunkNamePrefix}{x}, {y}, {z})");
					meshHolder.transform.SetParent(transform, false);
					meshHolder.layer = gameObject.layer;

					Chunk chunk = new Chunk(coord, centre, chunkSize, numPointsPerAxis, meshHolder);
					chunk.SetMaterial(material);
					chunks[i] = chunk;
					i++;
				}
			}
		}
	}


	void Create3DTexture(ref RenderTexture texture, int size, string name)
	{
		//
		var format = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
		if (texture == null || !texture.IsCreated() || texture.width != size || texture.height != size || texture.volumeDepth != size || texture.graphicsFormat != format)
		{
			//Debug.Log ("Create tex: update noise: " + updateNoise);
			if (texture != null)
			{
				texture.Release();
			}
			const int numBitsInDepthBuffer = 0;
			texture = new RenderTexture(size, size, numBitsInDepthBuffer);
			texture.graphicsFormat = format;
			texture.volumeDepth = size;
			texture.enableRandomWrite = true;
			texture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;


			texture.Create();
		}
		texture.wrapMode = TextureWrapMode.Repeat;
		texture.filterMode = FilterMode.Bilinear;
		texture.name = name;
	}

	bool HasRequiredComputeShaders()
	{
		if (meshCompute != null && densityCompute != null && blurCompute != null)
		{
			return true;
		}

		Debug.LogError("GenTest needs MarchingCubes, PlanetMap, and Blur compute shaders assigned.", this);
		return false;
	}

	void AssignDefaultComputeShaders()
	{
#if UNITY_EDITOR
		if (meshCompute == null)
		{
			meshCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Marching Cubes/Scripts/Compute/MarchingCubes.compute");
		}

		if (densityCompute == null)
		{
			densityCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Marching Cubes/Scripts/Compute/PlanetMap.compute");
		}

		if (blurCompute == null)
		{
			blurCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Marching Cubes/Scripts/Compute/Blur.compute");
		}

#endif
	}



}
