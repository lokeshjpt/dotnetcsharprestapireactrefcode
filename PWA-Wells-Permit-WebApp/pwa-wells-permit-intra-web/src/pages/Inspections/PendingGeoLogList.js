import { searchPendingGeoLog } from '../../api/inspectionApi';
import DueInspectionList from './DueInspectionList';

// Pending GeoLog List — parity with pending_geo_list.jsp.
function PendingGeoLogList() {
  return (
    <DueInspectionList
      title="Pending GeoLog List"
      subtitle="Permits awaiting a geotechnical log."
      dueLabel="GeoLog Due Date"
      fetchFn={searchPendingGeoLog}
      itemNoun="permit"
      ariaLabel="Pending GeoLog list"
      hint="To upload a GeoLog, click the Application Id link to open the detail page."
    />
  );
}

export default PendingGeoLogList;
