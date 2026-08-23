export enum ListFilterEmit {
  UpdateModelValue = 'update:modelValue',
  Apply = 'apply',
  Clear = 'clear'
}

export enum ListPaginationEmit {
  PageChange = 'page-change',
  PageSizeChange = 'page-size-change'
}

export enum ListHeaderRowEmit {
  SortChange = 'sort-change',
  SortClear = 'sort-clear'
}

export enum ListFooterRowEmit {
  Reset = 'reset'
}

export enum ListViewEmit {
  QueryChange = 'query-change',
  FilterClear = 'filter-clear',
  SortClear = 'sort-clear',
  Reset = 'reset'
}
