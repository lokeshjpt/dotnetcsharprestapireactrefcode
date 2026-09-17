// History detail dates arrive from the API as ISO yyyy-MM-dd strings (CONVERT ..., 23). Legacy
// screens display them as mm/dd/yyyy, so format for read-only display; the <input type="date">
// fields bind to the raw ISO value directly.
export function fmtHistDate(iso) {
  if (!iso) return '';
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(iso).trim());
  return match ? `${match[2]}/${match[3]}/${match[1]}` : iso;
}

// Normalises an API value into the yyyy-MM-dd string an <input type="date"> expects.
export function toHistDateInput(iso) {
  if (!iso) return '';
  const match = /^(\d{4}-\d{2}-\d{2})/.exec(String(iso).trim());
  return match ? match[1] : '';
}
