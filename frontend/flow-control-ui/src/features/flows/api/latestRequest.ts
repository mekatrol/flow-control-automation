export const createLatestRequestGuard = () => {
  // Aborting fetch saves work, but a response may already be queued when a route
  // changes. A monotonically increasing generation also prevents that stale
  // response from replacing the graph for the newer route.
  let generation = 0;

  const begin = (): number => {
    generation += 1;
    return generation;
  };

  const isCurrent = (requestGeneration: number): boolean => requestGeneration === generation;

  const invalidate = (): void => {
    generation += 1;
  };

  return { begin, isCurrent, invalidate };
};
