// @vitest-environment jsdom

import { mount, flushPromises, type VueWrapper } from '@vue/test-utils';
import { defineComponent, type Ref } from 'vue';
import { createMemoryHistory, createRouter, type Router } from 'vue-router';
import { describe, expect, it } from 'vitest';

import { useServerListQuery } from '@/composables/useServerPagination';
import type { ListQuery, ListRow } from '@/models';

interface TestRow extends ListRow {
  name: string;
  status: string;
}

const defaults: ListQuery<TestRow> = {
  page: 1,
  pageSize: 10,
  filter: '',
  sort: { column: 'name', direction: 'asc' }
};

const mountComposable = async (
  url: string
): Promise<{
  query: Ref<ListQuery<TestRow>>;
  router: Router;
  setQuery: (value: ListQuery<TestRow>) => void;
  wrapper: VueWrapper;
}> => {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/', component: { template: '<div />' } }]
  });
  await router.push(url);
  await router.isReady();

  let query!: Ref<ListQuery<TestRow>>;
  let setQuery!: (value: ListQuery<TestRow>) => void;
  const wrapper = mount(
    defineComponent({
      setup() {
        ({ query, setQuery } = useServerListQuery<TestRow>({
          defaults,
          pageSizeOptions: [10, 25, 50],
          sortableColumns: ['name', 'status']
        }));
        return () => null;
      }
    }),
    { global: { plugins: [router] } }
  );
  await flushPromises();

  return { query, router, setQuery, wrapper };
};

describe('useServerListQuery', () => {
  it('reads valid list options from the URL', async () => {
    const { query, wrapper } = await mountComposable(
      '/?page=3&pageSize=25&filter=pump&sort=status&direction=desc'
    );

    expect(query.value).toEqual({
      page: 3,
      pageSize: 25,
      filter: 'pump',
      sort: { column: 'status', direction: 'desc' }
    });
    wrapper.unmount();
  });

  it('uses safe defaults and removes erroneous parameters', async () => {
    const { query, router, wrapper } = await mountComposable(
      '/?page=-4&pageSize=999&filter=ok&sort=unknown&direction=sideways'
    );

    expect(query.value).toEqual({ ...defaults, filter: 'ok' });
    expect(router.currentRoute.value.query).toEqual({ filter: 'ok' });
    wrapper.unmount();
  });

  it('updates the URL while omitting defaults and preserving unrelated parameters', async () => {
    const { router, setQuery, wrapper } = await mountComposable('/?tab=details');

    setQuery({
      page: 2,
      pageSize: 50,
      filter: 'valve',
      sort: { column: 'status', direction: 'desc' }
    });
    await flushPromises();

    expect(router.currentRoute.value.query).toEqual({
      tab: 'details',
      page: '2',
      pageSize: '50',
      filter: 'valve',
      sort: 'status',
      direction: 'desc'
    });

    setQuery(defaults);
    await flushPromises();
    expect(router.currentRoute.value.query).toEqual({ tab: 'details' });
    wrapper.unmount();
  });

  it('responds to browser history navigation', async () => {
    const { query, router, wrapper } = await mountComposable('/?page=2');

    await router.push('/?page=4&filter=sensor');
    await flushPromises();

    expect(query.value.page).toBe(4);
    expect(query.value.filter).toBe('sensor');
    wrapper.unmount();
  });

  it('preserves an explicitly cleared default sort', async () => {
    const { query, router, setQuery, wrapper } = await mountComposable('/');

    setQuery({ ...defaults, sort: null });
    await flushPromises();
    expect(router.currentRoute.value.query).toEqual({ sort: 'none' });

    await router.push('/?sort=none');
    await flushPromises();
    expect(query.value.sort).toBeNull();
    wrapper.unmount();
  });
});
