import { useCallback, useEffect, useState } from 'react';
import { searchApplications } from '../api/applicationApi';

const DEFAULT_PAGE_SIZE = 25;

function useApplicationList(initialFilters = { statusCode: 'PEND' }) {
  const [filters, setFiltersState] = useState(initialFilters);
  const [items, setItems] = useState([]);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSizeState] = useState(DEFAULT_PAGE_SIZE);
  const [totalCount, setTotalCount] = useState(0);
  const [sortBy, setSortBy] = useState('addDate');
  const [sortDir, setSortDir] = useState('desc');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const load = useCallback(async (
    nextFilters = filters,
    nextPage = page,
    nextSize = pageSize,
    nextSortBy = sortBy,
    nextSortDir = sortDir,
  ) => {
    setLoading(true);
    setError('');
    try {
      const data = await searchApplications({
        ...nextFilters,
        page: nextPage,
        pageSize: nextSize,
        sortBy: nextSortBy,
        sortDir: nextSortDir,
      });
      setItems(data.items || []);
      setTotalCount(data.totalCount ?? (data.items ? data.items.length : 0));
    } catch {
      setError('Unable to load applications.');
    } finally {
      setLoading(false);
    }
  }, [filters, page, pageSize, sortBy, sortDir]);

  useEffect(() => {
    load();
  }, [load]);

  // Changing the filters or page size always returns to the first page of results.
  const setFilters = useCallback((updater) => {
    setPage(1);
    setFiltersState(updater);
  }, []);

  const setPageSize = useCallback((size) => {
    setPage(1);
    setPageSizeState(size);
  }, []);

  // Toggle sort direction on the active column, otherwise sort the new column ascending; reset to
  // the first page so results stay consistent with the chosen order.
  const toggleSort = useCallback((key) => {
    setPage(1);
    setSortDir((prevDir) => (sortBy === key ? (prevDir === 'asc' ? 'desc' : 'asc') : 'asc'));
    setSortBy(key);
  }, [sortBy]);

  return {
    items,
    loading,
    error,
    filters,
    setFilters,
    page,
    setPage,
    pageSize,
    setPageSize,
    totalCount,
    sortBy,
    sortDir,
    toggleSort,
    reload: load,
  };
}

export default useApplicationList;
