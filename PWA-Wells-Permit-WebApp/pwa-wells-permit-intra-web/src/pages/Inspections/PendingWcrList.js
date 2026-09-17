import { searchPendingWcr } from '../../api/inspectionApi';
import DueInspectionList from './DueInspectionList';

// Pending WCR List — parity with pending_dwr_list.jsp.
function PendingWcrList() {
  return (
    <DueInspectionList
      title="Pending WCR List"
      subtitle="Permits awaiting a Well Completion Report."
      dueLabel="WCR Due Date"
      fetchFn={searchPendingWcr}
      itemNoun="permit"
      ariaLabel="Pending WCR list"
      hint="To enter a WCR, click the Application Id link to open the detail page."
    />
  );
}

export default PendingWcrList;
