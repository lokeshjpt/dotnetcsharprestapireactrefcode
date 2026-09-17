// Borehole (investigation / geo-probe) work categories collect a single set of Borehole
// Specifications (number of boreholes, hole diameter, max depth) instead of a per-well
// specifications table. Covers the "inv" and "invprb" category codes — mirrors the backend
// PermitDocumentService.BoreholeCategories = { "inv", "invprb" } and the legacy app_work_info.jsp
// branch (workCat == "inv" || workCat == "invprb").
export function isBoreholeCategory(cat) {
  return (cat || '').trim().toLowerCase().startsWith('inv');
}
