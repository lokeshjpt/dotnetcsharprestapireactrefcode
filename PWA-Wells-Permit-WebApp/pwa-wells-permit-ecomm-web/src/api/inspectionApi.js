import axiosInstance from './axiosInstance';

// Public inspection availability calendar used by the project-date pickers. Returns
// { validRangeStart, validRangeEnd, days: [{ date, available, reason }] } where reason is
// '' (available), 'H' (weekend/holiday), 'U' (blocked by PWA) or 'M' (max inspections reached).
export async function getInspectionCalendar(fromIso, toIso) {
  const { data } = await axiosInstance.get('/api/inspections/calendar', {
    params: { from: fromIso, to: toIso },
  });
  return data;
}
