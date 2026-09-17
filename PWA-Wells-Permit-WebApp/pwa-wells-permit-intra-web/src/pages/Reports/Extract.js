import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import Button from '../../components/UI/Button';
import Pagination from '../../components/UI/Pagination';
import SortableHeader from '../../components/UI/SortableHeader';
import Loader from '../../components/Loader/Loader';
import { getExtractReport } from '../../api/reportApi';
import { getCities, getWorkCategories, getWorkTypes } from '../../api/referenceApi';
import './Reports.css';

const DEFAULT_PAGE_SIZE = 25;

const COLUMNS = [
  { key: 'permitNumber', label: 'Permit#', sortable: true },
  { key: 'appId', label: 'Application ID', sortable: true },
  { key: 'projLocation', label: 'Project Site Location', sortable: true },
  { key: 'cityName', label: 'Project City', sortable: true },
  { key: 'businessName', label: 'Business Name', sortable: true },
  { key: 'workId', label: 'Work ID', sortable: true },
  { key: 'workCategory', label: 'Work Cat', sortable: true, value: (r) => r.workCatDesc || r.workCategory },
  { key: 'workType', label: 'Work Type', sortable: true, value: (r) => r.workDesc || r.workType },
  { key: 'histWorkType', label: 'Hist Work Type', sortable: true },
  { key: 'histWellUse', label: 'Hist Well Use', sortable: true },
  { key: 'workSpecsId', label: 'Work Specs' },
  { key: 'stateWellNum', label: 'State Well#', sortable: true },
  { key: 'latitude', label: 'Latitude' },
  { key: 'longitude', label: 'Longitude' },
  { key: 'tractNum', label: 'Tract #', sortable: true },
  { key: 'sectNum', label: 'Section #', sortable: true },
  { key: 'permitIssuedDate', label: 'Permit Issued Date', sortable: true },
  { key: 'permitStatus', label: 'Permit Status', sortable: true, value: (r) => r.permitStatusDesc || r.permitStatus },
];

const emptyFilters = {
  issueYear: '',
  permitFromDate: '',
  permitToDate: '',
  cityName: '',
  permitNum: '',
  permitStatus: '',
  workCategory: '',
  workType: '',
  histWorkType: '',
  tractNum: '',
  sectNum: '',
  histWellUse: '',
};

function csvCell(value) {
  const s = value == null ? '' : String(value).trim();
  if (/[",\n]/.test(s)) return `"${s.replace(/"/g, '""')}"`;
  return s;
}

function cellValue(column, row) {
  return column.value ? column.value(row) : row[column.key];
}

function Extract() {
  const [filters, setFilters] = useState(emptyFilters);
  const [cities, setCities] = useState([]);
  const [categories, setCategories] = useState([]);
  const [workTypes, setWorkTypes] = useState([]);
  const [rows, setRows] = useState(null);
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [totalCount, setTotalCount] = useState(0);
  const [sortBy, setSortBy] = useState('');
  const [sortDir, setSortDir] = useState('asc');

  useEffect(() => {
    getCities().then((d) => setCities(d || [])).catch(() => {});
    getWorkCategories().then((d) => setCategories(d || [])).catch(() => {});
    getWorkTypes().then((d) => setWorkTypes(d || [])).catch(() => {});
  }, []);

  const update = (key) => (event) => setFilters((current) => ({ ...current, [key]: event.target.value }));

  // A pageSize of 0 asks the API for every matching row (used by Export CSV).
  const buildPayload = ({ pageNum, size, sortByKey, sortDirVal }) => ({
    issueYear: filters.issueYear || null,
    permitFromDate: filters.permitFromDate || null,
    permitToDate: filters.permitToDate || null,
    cityName: filters.cityName || null,
    permitNum: filters.permitNum || null,
    permitStatus: filters.permitStatus || null,
    workCategory: filters.workCategory || null,
    workType: filters.workType || null,
    histWorkType: filters.histWorkType || null,
    tractNum: filters.tractNum || null,
    sectNum: filters.sectNum || null,
    histWellUse: filters.histWellUse || null,
    sortBy: sortByKey || null,
    sortDir: sortDirVal,
    page: pageNum,
    pageSize: size,
  });

  const run = async ({ pageNum = 1, size = pageSize, sortByKey = sortBy, sortDirVal = sortDir } = {}) => {
    setError('');
    setLoading(true);
    try {
      const data = await getExtractReport(buildPayload({ pageNum, size, sortByKey, sortDirVal }));
      setRows(data.items || []);
      setTotalCount(data.totalCount ?? (data.items ? data.items.length : 0));
      setPage(pageNum);
      setPageSize(size);
      setSortBy(sortByKey);
      setSortDir(sortDirVal);
    } catch {
      setError('Unable to run the extract report.');
      setRows(null);
      setTotalCount(0);
    } finally {
      setLoading(false);
    }
  };

  const handleSort = (key) => {
    const nextDir = key === sortBy ? (sortDir === 'asc' ? 'desc' : 'asc') : 'asc';
    run({ pageNum: 1, sortByKey: key, sortDirVal: nextDir });
  };

  const exportCsv = async () => {
    setError('');
    setExporting(true);
    try {
      // Export the full filtered set (not just the current page), in the active sort order.
      const data = await getExtractReport(buildPayload({ pageNum: 1, size: 0, sortByKey: sortBy, sortDirVal: sortDir }));
      const all = data.items || [];
      if (all.length === 0) return;
      const header = COLUMNS.map((c) => csvCell(c.label)).join(',');
      const body = all.map((row) => COLUMNS.map((c) => csvCell(cellValue(c, row))).join(',')).join('\n');
      const blob = new Blob([`${header}\n${body}`], { type: 'text/csv;charset=utf-8;' });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `wells-permit-extract-${new Date().toISOString().slice(0, 10)}.csv`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(url);
    } catch {
      setError('Unable to export the extract report.');
    } finally {
      setExporting(false);
    }
  };

  return (
    <div className="page-shell extract-page">
      {loading && <Loader />}
      <div className="panel">
        <div className="report-toolbar">
          <h1 className="page-title">Extract to Excel</h1>
          <Link className="report-back" to="/reports">← Back to reports</Link>
        </div>
        <p className="page-subtitle">Flat permit/well extract across current and historical records. Filter, then export to CSV for Excel.</p>

        <div className="report-filter">
          <div className="report-field">
            <label htmlFor="ex-year">Permit issued year</label>
            <input id="ex-year" className="form-control" inputMode="numeric" value={filters.issueYear} onChange={update('issueYear')} placeholder="e.g. 2023" />
          </div>
          <div className="report-field">
            <label htmlFor="ex-from">Issued from</label>
            <input id="ex-from" type="date" className="form-control" value={filters.permitFromDate} onChange={update('permitFromDate')} />
          </div>
          <div className="report-field">
            <label htmlFor="ex-to">Issued to</label>
            <input id="ex-to" type="date" className="form-control" value={filters.permitToDate} onChange={update('permitToDate')} />
          </div>
          <div className="report-field">
            <label htmlFor="ex-city">City</label>
            <select id="ex-city" className="form-control" value={filters.cityName} onChange={update('cityName')}>
              <option value="">All cities</option>
              {cities.map((c) => <option key={c.code} value={(c.label || '').trim()}>{(c.label || '').trim()}</option>)}
            </select>
          </div>
          <div className="report-field">
            <label htmlFor="ex-permit">Permit #</label>
            <input id="ex-permit" className="form-control" value={filters.permitNum} onChange={update('permitNum')} placeholder="Contains…" />
          </div>
          <div className="report-field">
            <label htmlFor="ex-status">Permit status</label>
            <input id="ex-status" className="form-control" value={filters.permitStatus} onChange={update('permitStatus')} placeholder="e.g. PCLSD" />
          </div>
          <div className="report-field">
            <label htmlFor="ex-cat">Work category</label>
            <select id="ex-cat" className="form-control" value={filters.workCategory} onChange={update('workCategory')}>
              <option value="">All categories</option>
              {categories.map((c) => <option key={c.code} value={(c.code || '').trim()}>{(c.label || c.code || '').trim()}</option>)}
            </select>
          </div>
          <div className="report-field">
            <label htmlFor="ex-type">Work type</label>
            <select id="ex-type" className="form-control" value={filters.workType} onChange={update('workType')}>
              <option value="">All types</option>
              {workTypes.map((t) => <option key={t.code} value={(t.code || '').trim()}>{(t.label || t.code || '').trim()}</option>)}
            </select>
          </div>
          <div className="report-field">
            <label htmlFor="ex-histwt">Hist work type</label>
            <input id="ex-histwt" className="form-control" value={filters.histWorkType} onChange={update('histWorkType')} placeholder="Contains…" />
          </div>
          <div className="report-field">
            <label htmlFor="ex-histwu">Hist well use</label>
            <input id="ex-histwu" className="form-control" value={filters.histWellUse} onChange={update('histWellUse')} placeholder="Contains…" />
          </div>
          <div className="report-field">
            <label htmlFor="ex-tract">Tract #</label>
            <input id="ex-tract" className="form-control" value={filters.tractNum} onChange={update('tractNum')} placeholder="Contains…" />
          </div>
          <div className="report-field">
            <label htmlFor="ex-sect">Section #</label>
            <input id="ex-sect" className="form-control" value={filters.sectNum} onChange={update('sectNum')} placeholder="Contains…" />
          </div>
          <div className="report-field report-field--actions">
            <Button onClick={() => run({ pageNum: 1 })} disabled={loading}>{loading ? 'Running…' : 'Run report'}</Button>
            <Button variant="secondary" onClick={exportCsv} disabled={exporting || !rows || totalCount === 0}>{exporting ? 'Exporting…' : 'Export CSV'}</Button>
          </div>
        </div>

        {error && <div className="alert alert-error">{error}</div>}

        {rows && (
          <>
            <div className="report-summary">
              <div className="report-stat">
                <span className="report-stat__label">Rows</span>
                <span className="report-stat__value">{totalCount}</span>
              </div>
            </div>
            <div className="table-scroll" role="region" aria-label="Extract results" tabIndex={0}>
              <table className="data-table">
                <thead>
                  <tr>
                    {COLUMNS.map((c) => (
                      c.sortable
                        ? <SortableHeader key={c.key} label={c.label} sortKey={c.key} sortBy={sortBy} sortDir={sortDir} onSort={handleSort} />
                        : <th key={c.key} scope="col">{c.label}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {rows.length === 0 ? (
                    <tr><td colSpan={COLUMNS.length} className="muted-text">No records matched the criteria.</td></tr>
                  ) : rows.map((row, idx) => (
                    <tr key={`${row.appId}-${row.permitNumber}-${row.workId}-${idx}`}>
                      {COLUMNS.map((c) => {
                        const v = cellValue(c, row);
                        return <td key={c.key}>{v == null ? '' : String(v).trim()}</td>;
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination
              page={page}
              pageSize={pageSize}
              totalCount={totalCount}
              onPageChange={(next) => run({ pageNum: next })}
              onPageSizeChange={(size) => run({ pageNum: 1, size })}
              loading={loading}
              itemLabel="row"
            />
          </>
        )}
      </div>
    </div>
  );
}

export default Extract;
