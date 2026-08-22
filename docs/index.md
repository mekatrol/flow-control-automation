# Flow Control Automation documentation

Flow Control Automation is a graphical automation system that compiles portable
flow programs on the server and runs them through the same Flow VM semantics on
the managed server and supported hardware controllers.

## Guides

- [Virtual points and execution contexts](guides/virtual-points.md)

## Architecture

- [Portable flow runtime](architecture/portable-flow-runtime.md)
- [PLC scan cycle](architecture/plc-scan-cycle.md)
- [Flow authoring and controller targets](architecture/flow-authoring-and-controller-targets.md)
- [Frontend hosting and API access](architecture/frontend-hosting-and-api-access.md)

## Reference

- [Virtual-points API](reference/virtual-points-api.md)
- [Point model](reference/point-model.md)
- [Configuration vocabulary v1](reference/configuration-vocabulary-v1.md)
- [UI flow schema](reference/ui-flow-schema.md)
- [Runtime API](reference/runtime-api.md)
- [Flow IL v1](reference/flow-il-v1.md)
- [Flow VM host ABI v1](reference/flow-vm-host-abi-v1.md)
- [Flow IL debugger](reference/flow-il-v1-debugger.md)
- [Flow IL security](reference/flow-il-security.md)
- [Flow simulator](reference/flow-simulator.md)
- [Controller protocol v1](reference/controller-protocol-v1.md)
- [Controller features](reference/controller-features.md)

## Operations

- [Virtual-point operations](operations/virtual-points.md)
- [Live-output commissioning](operations/live-output-commissioning.md)

## Development and testing

- [Repository development](../README.md#local-development)
- [Frontend development](development/frontend.md)
- [Contributor guidelines](development/contributor-guidelines.md)
- [Vue component development](development/vue-components.md)
- [Test documentation standard](development/test-documentation.md)
- [Controller development](development/controller.md)
- [Controller setup](development/controller-setup.md)
- [KC868-A16 board](development/boards/kincony-kc868-a16.md)
- [Database migrations](development/database-migrations.md)
- [Virtual-point testing](testing/virtual-points.md)
- [Flow IL fixtures](testing/flow-il-v1-fixtures.md)

## Design decisions

Architecture decision records are maintained in the
[decision index](decisions/index.md).

## Archived plans

Completed delivery plans are retained as historical context under
[archived implementation plans](archive/index.md). They are not normative
product documentation.
