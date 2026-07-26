/**
 * Creates standard automation attributes for UI components.
 *
 * This composable allows components to expose stable automation identifiers
 * without consumers needing to know the underlying DOM structure.
 *
 * The returned function generates an object that can be passed directly to
 * Vue's `v-bind` directive.
 *
 * Example:
 *
 * const automation = useAutomation('user-card');
 *
 * <button v-bind="automation()">
 * <button v-bind="automation('delete')">
 *
 * Produces:
 *
 * <button data-automation="user-card">
 * <button data-automation="user-card.delete">
 */

type AutomationAttributes = Record<string, string>;

const AUTOMATION_ATTRIBUTE = 'data-automation';

export const useAutomation = (base: string): ((suffix?: string) => AutomationAttributes) => {
  /*
   * Returns the automation attribute object for a component or one of its
   * child elements.
   *
   * @param suffix Optional child identifier.
   *
   * Examples:
   *
   * automation()
   * => { "data-automation": "search" }
   *
   * automation("input")
   * => { "data-automation": "search.input" }
   *
   * automation("clear")
   * => { "data-automation": "search.clear" }
   */

  return (suffix?: string): AutomationAttributes => {
    if (!base) {
      return {};
    }

    return {
      [AUTOMATION_ATTRIBUTE]: suffix === undefined ? base : `${base}.${suffix}`
    };
  };
};
