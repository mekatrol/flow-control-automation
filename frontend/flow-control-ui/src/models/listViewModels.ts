export type ListRowId = string | number;

export type ListSortDirection = 'asc' | 'desc';

export type ListColumnKey<TRow> = Extract<keyof TRow, string>;

export interface ListRow {
  id: ListRowId;
  automation: string;
}

export interface ListColumn<TRow extends ListRow> {
  key: ListColumnKey<TRow>;
  label: string;
  automation: string;
  sortable?: boolean;
  width?: string;
  align?: 'start' | 'center' | 'end';
}

export interface ListSort<TRow extends ListRow> {
  column: ListColumnKey<TRow>;
  direction: ListSortDirection;
}

export interface ListQuery<TRow extends ListRow> {
  page: number;
  pageSize: number;
  filter: string;
  sort: ListSort<TRow> | null;
}

export interface ListPageChange {
  page: number;
  pageSize: number;
}

export interface ListPaginationModel {
  page: number;
  pageSize: number;
  totalItems: number;
}

export interface ListCellContext<TRow extends ListRow> {
  row: TRow;
  column: ListColumn<TRow>;
  value: TRow[ListColumnKey<TRow>];
}
