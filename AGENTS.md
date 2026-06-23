# Instrucciones para agentes

## Contrato obligatorio

Antes de modificar gameplay, arquitectura de juego, inicializacion de sistemas o comportamiento runtime, hay que leer y respetar:

- `Docs/Contrato_Funcionamiento_Juego.md`

Ese documento es obligatorio. Si un cambio de codigo contradice el contrato, actualiza el contrato en el mismo cambio y deja la nueva decision escrita de forma explicita.

## Regla practica

- No introducir reglas de funcionamiento implicitas.
- No cambiar el flujo de inicializacion sin revisar el contrato.
- Si una decision sigue abierta, documentarla como pendiente en vez de asumirla como regla cerrada.
