import FormSection from '../../../components/FormSection/FormSection';
import Button from '../../../components/UI/Button';
import { formatDollar, isOtherDrillMethod, isBoreholeCategory } from '../referenceData';
import { workFee, workWellCount } from '../works';

function categoryName(options, code) {
  const list = options?.workCategories || [];
  const match = list.find((c) => c.code === code || c.workCategory === code);
  return (match && (match.label || match.workCatDesc)) || code || '';
}

const isBorehole = isBoreholeCategory;

// "Work(s) Requesting Permit" review list. Lists every work added to the project with Update/Delete
// and a running Total; a regular "+ Add Work Type" button in the card header opens the add modal.
function StepWorksReview({ formData, options, amountDue, onAddAnother, onEditWork, onDeleteWork }) {
  const works = formData.works || [];

  return (
    <FormSection
      title={`Work${works.length === 1 ? '' : 's'} Requesting Permit`}
      borderColor="l-blue"
      headerRight={<Button variant="primary" onClick={onAddAnother}>+ Add Work Type</Button>}
    >
      {works.length === 0 ? (
        <p className="text-muted">
          No work types have been added yet. Choose “+ Add Work Type” above to add one.
        </p>
      ) : (
        <table className="data-table works-review-table">
          <thead>
            <tr>
              <th scope="col">Project Work Type</th>
              <th scope="col">Driller</th>
              <th scope="col">Number of Wells / Boreholes</th>
              <th scope="col">Work Total</th>
              <th scope="col"><span className="sr-only">Actions</span></th>
            </tr>
          </thead>
          <tbody>
            {works.map((work, i) => {
              const label = isBorehole(work.workCat) ? 'Boreholes' : 'Wells';
              const method = isOtherDrillMethod(work.dmeth, work.dmethName)
                ? (work.dmethOth || 'Other')
                : (work.dmethName || work.dmeth || '');
              const typeText = [
                categoryName(options, work.workCat),
                work.workDesc || work.workType,
                work.wUseDesc || '',
              ].filter(Boolean).join(' - ');
              const drillerText = [
                [work.drillerName, work.drillerLic].filter(Boolean).join(' - '),
                method,
              ].filter(Boolean).join(' ');
              return (
                <tr key={i}>
                  <td>{typeText}</td>
                  <td>{drillerText || '—'}</td>
                  <td>
                    {workWellCount(work)} {label}
                    <div className="text-muted">
                      {formatDollar(work.workFeeRate)} per {work.workFeeUnit}
                    </div>
                  </td>
                  <td>{formatDollar(workFee(work))}</td>
                  <td>
                    <div className="works-review-actions">
                      <Button variant="default" onClick={() => onEditWork(i)}>Update</Button>
                      <Button variant="danger" onClick={() => onDeleteWork(i)}>Delete</Button>
                    </div>
                  </td>
                </tr>
              );
            })}
            {amountDue != null && (
              <tr className="works-review-total">
                <td colSpan={3} style={{ textAlign: 'right', fontWeight: 700 }}>Total</td>
                <td style={{ fontWeight: 700 }}>{formatDollar(amountDue)}</td>
                <td />
              </tr>
            )}
          </tbody>
        </table>
      )}
    </FormSection>
  );
}

export default StepWorksReview;
