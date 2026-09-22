import type { ListQuery, ListRow } from '@/models';

/** Values returned in the status field of a demo device. */
export type DemoDeviceStatus = 'Active' | 'Idle' | 'Offline' | 'Maintenance';

/**
 * The DTO returned by the mock backend.
 *
 * AppListView requires every row to implement ListRow, which means it must have
 * a stable string or number ID. The remaining fields are ordinary API data and
 * become valid, type-checked column keys in the view.
 */
export interface DemoDeviceRow extends ListRow {
  id: number;
  name: string;
  serialNumber: string;
  type: string;
  location: string;
  owner: string;
  status: DemoDeviceStatus;
  utilisation: number;
  lastSeen: string;
}

/**
 * A list endpoint accepts the same query shape that AppListView emits. In a real
 * HTTP client these values would normally become URL query parameters such as
 * `?page=2&pageSize=25&filter=sydney&sort=location&direction=asc`.
 */
export type DemoDeviceListQuery = ListQuery<DemoDeviceRow>;

/**
 * Page metadata must come from the backend along with the current page of rows.
 * In particular, `totalItems` is the count after server filtering but before
 * server pagination; the UI cannot calculate it from `items.length`.
 */
export interface DemoDevicePage {
  items: DemoDeviceRow[];
  /** Total records in the backing dataset before applying the current filter. */
  totalAvailableItems: number;
  /** Total records matching the current filter before applying pagination. */
  totalItems: number;
  page: number;
  pageSize: number;
  pageCount: number;
}

const deviceTypes = ['Flow meter', 'Pressure sensor', 'Valve controller', 'Temperature probe'];
const locations = ['Sydney', 'Melbourne', 'Brisbane', 'Perth', 'Adelaide', 'Newcastle'];
const owners = ['Operations', 'Facilities', 'Quality', 'Engineering', 'Production'];
const statuses: DemoDeviceStatus[] = ['Active', 'Idle', 'Offline', 'Maintenance'];

/**
 * This module-level array represents records held by a remote database. It is
 * intentionally private: neither the Pinia store nor the Vue page can access all
 * 207 records, so they cannot accidentally perform backend work in the browser.
 */
const databaseRows: DemoDeviceRow[] = Array.from({ length: 207 }, (_, index) => {
  const number = index + 1;
  const date = new Date(Date.UTC(2026, 8, 22, 6, 0));
  date.setUTCHours(date.getUTCHours() - index * 5);

  return {
    id: number,
    name: `Device ${String(number).padStart(3, '0')}`,
    serialNumber: `FC-${2026 + (index % 3)}-${String(1000 + number)}`,
    type: deviceTypes[index % deviceTypes.length]!,
    location: locations[(index * 5) % locations.length]!,
    owner: owners[(index * 3) % owners.length]!,
    status: statuses[(index * 7) % statuses.length]!,
    utilisation: (index * 17 + 23) % 101,
    lastSeen: date.toISOString()
  };
});

/**
 * Mimic network latency and AbortSignal support without making a real request.
 * Request cancellation matters when a user filters or changes pages quickly: an
 * older, slower response must not replace a newer one in the store.
 */
const waitForMockNetwork = (signal?: AbortSignal): Promise<void> =>
  new Promise((resolve, reject) => {
    const timeout = window.setTimeout(resolve, 250);

    const abort = (): void => {
      window.clearTimeout(timeout);
      reject(new DOMException('The request was aborted.', 'AbortError'));
    };

    if (signal?.aborted) {
      abort();
      return;
    }

    signal?.addEventListener('abort', abort, { once: true });
  });

/** Compare two rows using the requested, strongly typed column key. */
const compareRows = (
  left: DemoDeviceRow,
  right: DemoDeviceRow,
  query: DemoDeviceListQuery
): number => {
  if (!query.sort) return 0;

  const leftValue = left[query.sort.column];
  const rightValue = right[query.sort.column];
  const direction = query.sort.direction === 'asc' ? 1 : -1;

  if (typeof leftValue === 'number' && typeof rightValue === 'number') {
    return (leftValue - rightValue) * direction;
  }

  return (
    String(leftValue).localeCompare(String(rightValue), undefined, {
      numeric: true,
      sensitivity: 'base'
    }) * direction
  );
};

/**
 * A frontend-only implementation of a backend list endpoint.
 *
 * The operation order deliberately matches a typical database query:
 * 1. filter the full dataset;
 * 2. sort the filtered dataset;
 * 3. count matching records;
 * 4. select only the requested page;
 * 5. return page metadata and page rows.
 *
 * No filtering, sorting, or slicing should be repeated by callers. Replacing
 * this function with `fetch()` later should therefore require no view changes.
 */
const list = async (query: DemoDeviceListQuery, signal?: AbortSignal): Promise<DemoDevicePage> => {
  await waitForMockNetwork(signal);

  const filter = query.filter.trim().toLocaleLowerCase();
  const filteredRows = filter
    ? databaseRows.filter((row) =>
        [row.name, row.serialNumber, row.type, row.location, row.owner, row.status].some((value) =>
          value.toLocaleLowerCase().includes(filter)
        )
      )
    : databaseRows;

  // Sorting a copy protects the mock database's natural order between requests.
  const sortedRows = query.sort
    ? [...filteredRows].sort((a, b) => compareRows(a, b, query))
    : filteredRows;
  const pageSize = Math.max(1, Math.trunc(query.pageSize));
  const pageCount = Math.max(1, Math.ceil(sortedRows.length / pageSize));
  const page = Math.min(Math.max(1, Math.trunc(query.page)), pageCount);
  const start = (page - 1) * pageSize;

  return {
    // Return copies to mimic deserialized JSON and keep the database private.
    items: sortedRows.slice(start, start + pageSize).map((row) => ({ ...row })),
    totalAvailableItems: databaseRows.length,
    totalItems: sortedRows.length,
    page,
    pageSize,
    pageCount
  };
};

/** Public API surface consumed by the store, analogous to a real API client. */
export const listViewDemoApi = { list };
