Flows can run in the controller in this project, or they can be deployed to a physical controller

When flows are deployed to a physical controller they may have limited point types and point capabilities.

For example, a very basic controller may not have override features, not might it have every point type of flow function type.

In these cases a controller template is used to define the apabilities of the controller the flow is being deployed to. The template guides what points, point features, functions etc a flow can have when a particular controller is being targetted.

The default target is this flow control system, meaning all features can be used.

When expaling this desription, create flow-implementation-plan.md. Look at sibling point-implmentation-plan.md. This plan should have all requirements for sematic html, comments, wcag compliance, e2e and unit testing and so forth.

The front and backend will need a way of defining controller templates. Initially this should just be as a yaml file and the user can edit taml rather than a graphical UI form field type approach. the yaml should be validated.

Start with the default yaml for running in this project app which the user can view as an example, however the default should be readonly and include all features and functions.

Also if flow-implementation-plan.md and point-implmentation-plan.md make sens as one implmentation then update point-implmentation-plan.md and rename to implmentation-plan.md covering all implmentation phases. This may be needed if points and flow implementation need to be implmentated concurrently at each step to allow some dependant features between the two.

Then once the plan is written update this document to fully describe how flows work so that an AI agent can refer to it in the future, later we'll generate user doecumentation from these ms files in .codex.

