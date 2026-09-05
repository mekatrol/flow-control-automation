import { describe, expect, it } from 'vitest';

import { flowNodeTypes, getNodeTypeDefinition } from '@/features/flows/nodeTypes';
import { flowTutorials, parseTutorial } from '@/features/flows/tutorialCatalogue';

describe('flow tutorial catalogue', () => {
  /**
   * Purpose: Prevents an executable palette function from shipping without discoverable guidance.
   * Description: Compares canonical executable kinds with strict tutorial metadata and fixtures.
   */
  it('contains one current-schema tutorial for every executable function', () => {
    // Arrange: Derive coverage from the same canonical registry used by the palette.
    const executableNodeTypes = flowNodeTypes
      .filter((nodeType) => getNodeTypeDefinition(nodeType).executable)
      .sort();

    // Act: Parse every repository-owned entry through the strict current-version parser.
    const tutorialNodeTypes = flowTutorials
      .map((tutorial) => parseTutorial(tutorial).nodeType)
      .sort();

    // Assert: Coverage is exact, without unknown or duplicate function tutorials.
    expect(tutorialNodeTypes).toEqual(executableNodeTypes);
    expect(new Set(tutorialNodeTypes).size).toBe(tutorialNodeTypes.length);
  });

  /**
   * Purpose: Protects immutable canonical tutorial fixture identity.
   * Description: Verifies each fixture contains its advertised function and a disposable flow identity.
   */
  it('uses disposable ordinary flow fixtures containing the advertised block', () => {
    for (const tutorial of flowTutorials) {
      expect(tutorial.flow.id).toBe(`tutorial-${tutorial.nodeType}`);
      expect(tutorial.flow.nodes.some((node) => node.nodeType === tutorial.nodeType)).toBe(true);
      expect(tutorial.guidance.length).toBeGreaterThan(0);
    }
  });
});
