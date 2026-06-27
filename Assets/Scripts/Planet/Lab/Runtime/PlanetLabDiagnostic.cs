using System;
using UnityEngine;

namespace MarchingCubesPlanet.Lab
{
    [Serializable]
    public struct PlanetLabDiagnostic
    {
        public PlanetLabDiagnosticSeverity severity;
        public string title;

        [TextArea]
        public string probableCause;

        [TextArea]
        public string recommendedAction;

        [TextArea]
        public string relatedMetrics;

        public static PlanetLabDiagnostic Ok(string title, string relatedMetrics)
        {
            return new PlanetLabDiagnostic
            {
                severity = PlanetLabDiagnosticSeverity.OK,
                title = title,
                probableCause = "No issue detected.",
                recommendedAction = "Continue with the next validation step.",
                relatedMetrics = relatedMetrics
            };
        }

        public static PlanetLabDiagnostic Warning(string title, string cause, string action, string metrics)
        {
            return new PlanetLabDiagnostic
            {
                severity = PlanetLabDiagnosticSeverity.Warning,
                title = title,
                probableCause = cause,
                recommendedAction = action,
                relatedMetrics = metrics
            };
        }

        public static PlanetLabDiagnostic Critical(string title, string cause, string action, string metrics)
        {
            return new PlanetLabDiagnostic
            {
                severity = PlanetLabDiagnosticSeverity.Critical,
                title = title,
                probableCause = cause,
                recommendedAction = action,
                relatedMetrics = metrics
            };
        }
    }
}
