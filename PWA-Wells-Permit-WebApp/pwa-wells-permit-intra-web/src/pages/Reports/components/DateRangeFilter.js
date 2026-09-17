import { DATE_PRESETS } from '../../../utils/reportDates';

// Shared date-range filter used by the Reconciliation / Completed Work / Completed Inspections
// reports. Mirrors the legacy rpt_menu date-preset dropdown plus specific-date inputs.
function DateRangeFilter({ preset, onPresetChange, stDate, endDate, onStDateChange, onEndDateChange }) {
  return (
    <>
      <div className="report-field">
        <label htmlFor="report-date-preset">Date range</label>
        <select
          id="report-date-preset"
          className="form-control"
          value={preset}
          onChange={(event) => onPresetChange(event.target.value)}
        >
          {DATE_PRESETS.map((option) => (
            <option key={option.code} value={option.code}>{option.label}</option>
          ))}
        </select>
      </div>
      {preset === 'sp' && (
        <>
          <div className="report-field">
            <label htmlFor="report-start-date">Start date</label>
            <input
              id="report-start-date"
              type="date"
              className="form-control"
              value={stDate}
              onChange={(event) => onStDateChange(event.target.value)}
            />
          </div>
          <div className="report-field">
            <label htmlFor="report-end-date">End date</label>
            <input
              id="report-end-date"
              type="date"
              className="form-control"
              value={endDate}
              onChange={(event) => onEndDateChange(event.target.value)}
            />
          </div>
        </>
      )}
    </>
  );
}

export default DateRangeFilter;
