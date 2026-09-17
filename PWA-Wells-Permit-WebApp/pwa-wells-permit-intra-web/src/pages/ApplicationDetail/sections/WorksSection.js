import { Fragment, useCallback, useEffect, useState } from 'react';
import DetailSection, { DetailField } from '../../../components/DetailSection/DetailSection';
import Button from '../../../components/UI/Button';
import { useToast } from '../../../components/UI/Toaster/ToastProvider';
import { formatStatus, statusPillClass } from '../../../constants/statusCodes';
import { isBoreholeCategory } from '../../../constants/workCategories';
import { countWells, workCalcAmount } from '../../../utils/feeCalc';
import { getApplication, cancelWork, deleteWork } from '../../../api/applicationApi';
import { getConditions } from '../../../api/conditionApi';
import EditWorkModal from './edit/EditWorkModal';
import AddWorkModal from './edit/AddWorkModal';
import EditWcrModal from './edit/EditWcrModal';
import EditGeoLogModal from './edit/EditGeoLogModal';
import WorkConditionsModal from './edit/WorkConditionsModal';

function money(n) {
  return `$${Number(n || 0).toFixed(2)}`;
}

function WorksSection({ application, onUpdated }) {
  const showToast = useToast();
  const works = application?.works || [];
  const workTypes = application?.workTypes || [];
  const appId = application?.appId;
  const [editWorkId, setEditWorkId] = useState(null);
  const [wcrWorkId, setWcrWorkId] = useState(null);
  const [geoLogWorkId, setGeoLogWorkId] = useState(null);
  const [condWorkId, setCondWorkId] = useState(null);
  const [showAddWork, setShowAddWork] = useState(false);
  const [busyWorkId, setBusyWorkId] = useState(null);
  const [expandedSpecs, setExpandedSpecs] = useState({});
  // Per-work permit-conditions data (work-type-scoped master list + applied conditions + status),
  // keyed by workId — drives the per-row "Apply / Edit Conditions" button and the conditions popup.
  const [condEntries, setCondEntries] = useState([]);

  const loadConditions = useCallback(() => {
    if (!appId) return;
    getConditions(appId)
      .then((data) => setCondEntries(data?.works || []))
      .catch(() => setCondEntries([]));
  }, [appId]);

  // Re-fetch the per-work conditions whenever the set of works (or their statuses) changes, so a newly
  // added work immediately shows its "Apply Conditions" button — its conditions entry would otherwise
  // be missing from condEntries until a full page reload.
  const worksSignature = works.map((w) => `${w.workId}:${(w.statusCode || '').toUpperCase()}`).join('|');
  useEffect(() => { loadConditions(); }, [loadConditions, worksSignature]);

  const toggleSpecs = (workId) =>
    setExpandedSpecs((cur) => ({ ...cur, [workId]: !cur[workId] }));

  // "Enter WCR" / "Enter GeoLog" are post-approval data entry — the buttons are hidden entirely until
  // the application status is APPRV (the legacy search_detail.jsp shows them disabled; we hide them).
  const isApproved = (application?.statusCode || '').toUpperCase() === 'APPRV';
  // A cancelled or approved application has a fixed work list — hide Add/Cancel/Delete, mirroring the
  // legacy search_detail.jsp guards.
  const isTerminal = ['CAN', 'APPRV'].includes((application?.statusCode || '').toUpperCase());

  const handleSaved = async (updated) => {
    if (updated) {
      onUpdated?.(updated);
      return;
    }
    // File uploads (WCR image / geolog) don't return the whole application — refetch so the stored
    // file names propagate into the detail view.
    if (application?.appId) {
      try {
        const fresh = await getApplication(application.appId);
        if (fresh) onUpdated?.(fresh);
      } catch {
        /* non-fatal: the modal already reflects the upload locally */
      }
    }
  };

  const handleCancelWork = async (workId) => {
    if (!window.confirm('Cancel this work? This cannot be undone.')) return;
    setBusyWorkId(workId);
    try {
      const updated = await cancelWork(application.appId, workId);
      if (updated) onUpdated?.(updated);
      showToast('Work cancelled.', 'success');
    } catch (err) {
      const msg = err?.response?.status === 409
        ? 'This work can no longer be cancelled.'
        : 'Unable to cancel the work.';
      showToast(msg, 'error');
    } finally {
      setBusyWorkId(null);
    }
  };

  const handleDeleteWork = async (workId) => {
    if (!window.confirm('Delete this work? This cannot be undone.')) return;
    setBusyWorkId(workId);
    try {
      const updated = await deleteWork(application.appId, workId);
      if (updated) onUpdated?.(updated);
      showToast('Work deleted.', 'success');
    } catch (err) {
      const serverMsg = err?.response?.status === 409 ? err?.response?.data : null;
      showToast(typeof serverMsg === 'string' && serverMsg ? serverMsg : 'Unable to delete the work.', 'error');
    } finally {
      setBusyWorkId(null);
    }
  };

  const editingWork = works.find((w) => w.workId === editWorkId) || null;
  const wcrWork = works.find((w) => w.workId === wcrWorkId) || null;
  const geoLogWork = works.find((w) => w.workId === geoLogWorkId) || null;
  const condByWorkId = condEntries.reduce((map, e) => { map[e.workId] = e; return map; }, {});
  const condEntry = condEntries.find((e) => e.workId === condWorkId) || null;
  const condDetail = works.find((w) => w.workId === condWorkId) || {};

  // After conditions are applied in the popup, refresh both the per-work conditions list (so the
  // button flips Apply -> Edit) and the whole application (so status pills / gate advance).
  const handleConditionsSaved = async (fresh) => {
    setCondEntries(fresh?.works || []);
    setCondWorkId(null);
    if (!appId) return;
    try {
      const app = await getApplication(appId);
      if (app) onUpdated?.(app);
    } catch {
      /* non-fatal: the list already reflects the new status */
    }
  };

  const addWorkModal = showAddWork ? (
    <AddWorkModal
      application={application}
      onClose={() => setShowAddWork(false)}
      onSaved={handleSaved}
    />
  ) : null;

  const addWorkBtn = !isTerminal ? (
    <Button variant="default" onClick={() => setShowAddWork(true)}>Add Work</Button>
  ) : null;

  if (!works.length) {
    return (
      <>
        <DetailSection title="Work Requesting Permit" actions={addWorkBtn}>
          <div className="detail-col detail-col--full">
            {workTypes.length ? (
              workTypes.map((wt) => <DetailField key={wt} label="Work Type">{wt}</DetailField>)
            ) : (
              <p className="muted-text" style={{ margin: 0 }}>No work items were returned by the API.</p>
            )}
          </div>
        </DetailSection>
        {addWorkModal}
      </>
    );
  }

  return (
    <>
      <DetailSection title="Work Requesting Permit" actions={addWorkBtn}>
        <div className="detail-col detail-col--full" style={{ gridColumn: '1 / -1' }}>
          <table className="data-table works-table">
            <thead>
              <tr>
                <th className="works-table__expander" aria-label="Expand" />
                <th>Project Work Type</th>
                <th>Well Use</th>
                <th>Driller</th>
                <th>No. of Wells</th>
                <th>Work Total</th>
                <th>Status</th>
                <th className="works-table__actions-col">Actions</th>
              </tr>
            </thead>
            <tbody>
              {works.map((work) => {
                const wells = countWells(work);
                const rate = work.workFeeRate;
                const total = rate != null ? workCalcAmount(work) : null;
                const workType = [
                  work.workCategoryDesc || work.workCategory,
                  work.workTypeDesc || work.workType,
                ].filter(Boolean).join(' - ');
                const driller = work.drillerName
                  ? `${work.drillerName}${work.drillerLicenseNum ? ` - Lic# ${work.drillerLicenseNum}` : ''}`
                  : '—';
                const isCancelled = (work.statusCode || '').toUpperCase() === 'CAN';
                const cond = condByWorkId[work.workId];
                const condPendc = (cond?.statusCode || work.statusCode || '').toUpperCase() === 'PENDC';
                const specCount = (work.specs || []).length;
                const specsOpen = Boolean(expandedSpecs[work.workId]);
                const isBorehole = isBoreholeCategory(work.workCategory);

                return (
                  <Fragment key={work.workId}>
                    <tr>
                      <td className="works-table__expander">
                        {specCount > 0 ? (
                          <button
                            type="button"
                            className="specs-toggle specs-toggle--icon"
                            aria-expanded={specsOpen}
                            aria-label={`${specsOpen ? 'Hide' : 'Show'} ${isBorehole ? 'boreholes' : 'wells'} for ${workType}`}
                            title={`${specsOpen ? 'Hide' : 'Show'} ${isBorehole ? 'boreholes' : 'wells'} (${specCount})`}
                            onClick={() => toggleSpecs(work.workId)}
                          >
                            <span className="specs-toggle__chevron" aria-hidden="true">{specsOpen ? '\u25BC' : '\u25B6'}</span>
                          </button>
                        ) : null}
                      </td>
                      <td>{workType || '—'}</td>
                      <td>{work.wellUseDesc || work.wellUseType || '—'}</td>
                      <td>{driller}</td>
                      <td>{wells ? `${wells}${rate != null ? ` - ${money(rate)} per ${work.workFeeUnit || 'well'}` : ''}` : '—'}</td>
                      <td>{total != null ? money(total) : '—'}</td>
                      <td><span className={statusPillClass(work.statusCode)}>{formatStatus(work.statusCode)}</span></td>
                      <td>
                        <div className="works-table__actions">
                          <Button variant="default" onClick={() => setEditWorkId(work.workId)}>Edit</Button>
                          {!isCancelled && cond && (
                            <Button variant={condPendc ? 'primary' : 'default'} onClick={() => setCondWorkId(work.workId)}>
                              {condPendc ? 'Apply Conditions' : 'Edit Conditions'}
                            </Button>
                          )}
                          {!isTerminal && !isCancelled && (
                            <Button variant="default" onClick={() => handleCancelWork(work.workId)} disabled={busyWorkId === work.workId}>Cancel Work</Button>
                          )}
                          {!isTerminal && works.length > 1 && (
                            <Button variant="danger" onClick={() => handleDeleteWork(work.workId)} disabled={busyWorkId === work.workId}>Delete</Button>
                          )}
                          {!isCancelled && isApproved && (
                            <>
                              <Button variant="default" onClick={() => setWcrWorkId(work.workId)}>Enter WCR</Button>
                              <Button variant="default" onClick={() => setGeoLogWorkId(work.workId)}>Enter GeoLog</Button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                    {specsOpen && specCount > 0 && (
                      <tr className="works-table__detail">
                        <td />
                        <td colSpan={7}>
                          <div className="works-specs">
                            <div className="works-specs__title">{isBorehole ? 'Borehole Specifications' : 'Well Specifications'} ({specCount})</div>
                            {isBorehole ? (
                              <table className="data-table">
                                <thead>
                                  <tr>
                                    <th># Boreholes</th>
                                    <th>Hole diam (in)</th>
                                    <th>Max depth (ft)</th>
                                  </tr>
                                </thead>
                                <tbody>
                                  {work.specs.map((spec) => (
                                    <tr key={spec.workSpecsId}>
                                      <td>{spec.drillCount ?? '—'}</td>
                                      <td>{spec.holeDiamIn ?? '—'}</td>
                                      <td>{spec.maxDepthFt ?? '—'}</td>
                                    </tr>
                                  ))}
                                </tbody>
                              </table>
                            ) : (
                              <table className="data-table">
                                <thead>
                                  <tr>
                                    <th>Well #</th>
                                    <th>Max depth (ft)</th>
                                    <th>Hole diam (in)</th>
                                    <th>Casing diam (in)</th>
                                    <th>Latitude</th>
                                    <th>Longitude</th>
                                    <th>Permit #</th>
                                  </tr>
                                </thead>
                                <tbody>
                                  {work.specs.map((spec) => (
                                    <tr key={spec.workSpecsId}>
                                      <td>{spec.ownerWellNum || '—'}</td>
                                      <td>{spec.maxDepthFt ?? '—'}</td>
                                      <td>{spec.holeDiamIn ?? '—'}</td>
                                      <td>{spec.casingDiamIn ?? '—'}</td>
                                      <td>{spec.latitude || '—'}</td>
                                      <td>{spec.longitude || '—'}</td>
                                      <td>{spec.permitNum || '—'}</td>
                                    </tr>
                                  ))}
                                </tbody>
                              </table>
                            )}
                          </div>
                        </td>
                      </tr>
                    )}
                  </Fragment>
                );
              })}
            </tbody>
          </table>
        </div>
      </DetailSection>

      {editingWork && (
        <EditWorkModal
          application={application}
          work={editingWork}
          onClose={() => setEditWorkId(null)}
          onSaved={handleSaved}
        />
      )}

      {wcrWork && (
        <EditWcrModal
          application={application}
          work={wcrWork}
          onClose={() => setWcrWorkId(null)}
          onSaved={handleSaved}
        />
      )}

      {geoLogWork && (
        <EditGeoLogModal
          application={application}
          work={geoLogWork}
          onClose={() => setGeoLogWorkId(null)}
          onSaved={handleSaved}
        />
      )}

      {condEntry && (
        <WorkConditionsModal
          appId={appId}
          entry={condEntry}
          detail={condDetail}
          onClose={() => setCondWorkId(null)}
          onSaved={handleConditionsSaved}
        />
      )}

      {addWorkModal}
    </>
  );
}

export default WorksSection;
