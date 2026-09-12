export const formatPointTestValue = (value: unknown, units?: unknown): string => {
  if (value === null || value === undefined) return 'Unavailable';

  const displayedValue = typeof value === 'string' ? value : JSON.stringify(value);
  const displayedUnits = typeof units === 'string' ? units.trim() : '';
  return `${displayedValue}${displayedUnits ? ` ${displayedUnits}` : ''}`;
};
