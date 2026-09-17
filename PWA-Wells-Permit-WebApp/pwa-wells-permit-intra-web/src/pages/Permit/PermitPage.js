import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { getApplication, getPermitInfo } from '../../api/applicationApi';
import { getPayment } from '../../api/paymentApi';
import { getConditions } from '../../api/conditionApi';
import { countWells, workCalcAmount, baseFee } from '../../utils/feeCalc';
import { isApprovedStatus } from '../../constants/statusCodes';
import { formatPaymentType } from '../../constants/paymentTypes';
import './PermitPage.css';

const LOGO_SRC = `${process.env.PUBLIC_URL}/assets/permit-logo.png`;

function money(n) {
  return `$${Number(n || 0).toFixed(2)}`;
}

function fmtDate(value) {
  if (!value) return '';
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? '' : d.toLocaleDateString('en-US');
}

function joinName(...parts) {
  return parts.map((p) => (p || '').trim()).filter(Boolean).join(' ');
}

function cityStateZip(city, state, zip) {
  const left = [city, state].map((p) => (p || '').trim()).filter(Boolean).join(', ');
  return [left, (zip || '').trim()].filter(Boolean).join('  ');
}

function fullAddress(street, street2, city, state, zip) {
  const line1 = [street, street2].map((p) => (p || '').trim()).filter(Boolean).join(' ');
  return [line1, cityStateZip(city, state, zip)].filter(Boolean).join(', ');
}

// Printable Water Resources well permit — a faithful, print-ready rendering of the legacy intra
// DisplayPdf permit. Opened in a new browser tab from the Application Detail page ("Preview Permit"
// before approval / "View Permit/Reprint" after) and printed / saved to PDF by the browser.
function PermitPage() {
  const { appId } = useParams();
  const [state, setState] = useState({ loading: true, error: '', application: null, payment: null, permit: null, conditions: null });

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const [application, payment, permit, conditions] = await Promise.all([
          getApplication(appId),
          getPayment(appId).catch(() => null),
          getPermitInfo(appId).catch(() => null),
          getConditions(appId).catch(() => null),
        ]);
        if (active) setState({ loading: false, error: '', application, payment, permit, conditions });
      } catch (err) {
        if (active) setState({ loading: false, error: 'Unable to load the permit for this application.', application: null, payment: null, permit: null, conditions: null });
      }
    })();
    return () => { active = false; };
  }, [appId]);

  const { loading, error, application, payment, permit, conditions } = state;

  useEffect(() => {
    document.title = application ? `Well Permit ${application.appId}` : 'Well Permit';
  }, [application]);

  if (loading) {
    return <div className="permit-shell"><p>Loading permit…</p></div>;
  }
  if (error || !application) {
    return <div className="permit-shell"><p className="permit-error">{error || 'Application not found.'}</p></div>;
  }

  const approved = isApprovedStatus(application.statusCode);
  const works = application.works || [];
  const serviceCharge = Number(payment?.serviceCharge || 0);
  const fine = Number(payment?.fineAmount || 0);
  const authAmount = Number(payment?.authAmount || 0);
  // Total Due includes any fine assessed during review, matching the emailed permit PDF.
  const totalDue = (authAmount > 0 ? authAmount : baseFee(works) + serviceCharge) + fine;
  const paid = Number(payment?.paidAmount || 0);
  const payStatus = String(payment?.statusCode || '').toUpperCase();

  const permitLines = permit?.permits || [];
  const permitNumbers = permitLines.map((p) => p.permitNumber).filter(Boolean);
  let permitSummary = '';
  if (permitNumbers.length > 1) {
    permitSummary = `${permitNumbers[0]} to ${permitNumbers[permitNumbers.length - 1]}`;
  } else if (permitNumbers.length === 1) {
    permitSummary = permitNumbers[0];
  }
  const permitsByWork = permitLines.reduce((map, line) => {
    (map[line.workId] = map[line.workId] || []).push(line);
    return map;
  }, {});

  // Conditions are tracked per work (an application with multiple works has one condition set per
  // work). Flatten every work's applied conditions into a single de-duplicated permit list. A work
  // still Pending Conditions has an empty selection and so contributes nothing.
  const selectedConditions = [];
  const seenConditionText = new Set();
  (conditions?.works || []).forEach((work) => {
    const condByType = new Map();
    (work.available || []).forEach((c) => condByType.set(c.code, c.label || c.description || c.code));
    (work.selected || []).forEach((c) => {
      const text = c.otherDesc || condByType.get(c.conditionType) || c.conditionType;
      const key = String(text || '').trim().toLowerCase();
      if (key && !seenConditionText.has(key)) {
        seenConditionText.add(key);
        selectedConditions.push({ idx: selectedConditions.length + 1, text });
      }
    });
  });

  let payStatusLabel = '';
  if (payStatus === 'PAYFL') payStatusLabel = '** PAYMENT FAILED **';
  else if (payStatus === 'EXMPT') payStatusLabel = 'PAYMENT EXEMPT';
  else if (paid > 0 && paid >= totalDue) payStatusLabel = 'PAID IN FULL';
  else payStatusLabel = 'PAYMENT DUE';
  const paidInFull = payStatusLabel === 'PAID IN FULL';

  const applicantName = joinName(application.appBusinessName ? `${application.appBusinessName} -` : '', application.appFirstName, application.appLastName);
  const ownerName = joinName(application.ownerFirstName, application.ownerLastName);
  const clientName = joinName(application.clientFirstName, application.clientLastName);
  const contactName = joinName(application.contactFirstName, application.contactLastName);

  const applicantAddr = fullAddress(application.appAddrStreet, application.appAddrStreet2, application.appAddrCity, application.appAddrState, application.appAddrZip);
  const ownerAddr = fullAddress(application.ownerAddrStreet, null, application.ownerAddrCity, application.ownerAddrState, application.ownerAddrZip);
  const clientAddr = fullAddress(application.clientAddrStreet, null, application.clientAddrCity, application.clientAddrState, application.clientAddrZip);

  const paidByLabel = approved ? 'Paid By' : 'Payment Type';

  return (
    <main className="permit-shell">
      <div className="permit-actions">
        <button type="button" className="btn btn-primary" onClick={() => window.print()}>Print / Save as PDF</button>
        <button type="button" className="btn btn-default" onClick={() => window.close()}>Close</button>
        {!approved && <span className="permit-preview-flag">PREVIEW — not yet approved</span>}
      </div>

      <div className={`permit-doc${approved ? '' : ' permit-doc--preview'}`}>
        {!approved && <div className="permit-watermark-band" aria-hidden="true"><span>PREVIEW</span></div>}

        <header className="permit-head">
          <h1 className="permit-title">Alameda County Public Works Agency</h1>
          <div className="permit-subtitle">Water Resources — Well Permit</div>
          <div className="permit-masthead">
            <img className="permit-logo" src={LOGO_SRC} alt="Alameda County Public Works Agency" />
            <div className="permit-office">
              <div>399 Elmhurst Street, Hayward, CA 94544-1395</div>
              <div>Telephone: (510) 670-6633 &nbsp;·&nbsp; Fax: (510) 782-1939</div>
            </div>
          </div>
        </header>

        {approved ? (
          <section className="permit-approval">
            <div className="pa-left">
              <span className="lbl">Application Approved on:</span> {fmtDate(permit?.approvedDate)} By {permit?.approvedBy || '—'}
            </div>
            <div className="pa-right">
              {permitSummary && <div><span className="lbl">Permit Numbers:</span> {permitSummary}</div>}
              <div><span className="lbl">Permits Valid</span> from {fmtDate(application.projStartDate)} to {fmtDate(application.projEndDate)}</div>
            </div>
          </section>
        ) : (
          <section className="permit-preview-note">
            This is a <strong>preview</strong> of the well permit. It is <strong>not yet approved</strong> and is not valid for drilling.
          </section>
        )}

        <section className="permit-grid">
          <div className="pg-row"><span className="lbl">Application Id:</span> <span>{application.appId}</span></div>
          <div className="pg-row"><span className="lbl">City of Project Site:</span> <span>{application.siteCityName || application.siteCityCode || ''}</span></div>
          <div className="pg-row span2"><span className="lbl">Site Location:</span> <span>{application.siteLocation || ''}</span></div>
          <div className="pg-row"><span className="lbl">Project Start Date:</span> <span>{fmtDate(application.projStartDate)}</span></div>
          <div className="pg-row"><span className="lbl">Completion Date:</span> <span>{fmtDate(application.projEndDate)}</span></div>
          <div className="pg-row span2"><span className="lbl">Agency Representative:</span> <span>Alameda County Water Resources Section &nbsp;·&nbsp; (510) 670-6633</span></div>
        </section>

        <section className="permit-parties">
          <div className="party">
            <div className="party-main">
              <div><span className="lbl">Applicant:</span> {applicantName || '—'}</div>
              {applicantAddr && <div className="muted">{applicantAddr}</div>}
            </div>
            <div className="party-phone">
              {application.appPhone && <div><span className="lbl">Phone:</span> {application.appPhone}</div>}
            </div>
          </div>

          <div className="party">
            <div className="party-main">
              <div><span className="lbl">Property Owner:</span> {ownerName || '—'}</div>
              {ownerAddr && <div className="muted">{ownerAddr}</div>}
            </div>
            <div className="party-phone">
              {application.ownerPhone && <div><span className="lbl">Phone:</span> {application.ownerPhone}</div>}
            </div>
          </div>

          <div className="party">
            <div className="party-main">
              <div><span className="lbl">Client:</span> {clientName || '** same as Property Owner **'}</div>
              {clientName && clientAddr && <div className="muted">{clientAddr}</div>}
            </div>
            <div className="party-phone">
              {clientName && application.clientPhone && <div><span className="lbl">Phone:</span> {application.clientPhone}</div>}
            </div>
          </div>

          {contactName && (
            <div className="party">
              <div className="party-main">
                <div><span className="lbl">Contact:</span> {contactName}</div>
                {application.contactEmail && <div className="muted">{application.contactEmail}</div>}
              </div>
              <div className="party-phone">
                {application.contactPhone && <div><span className="lbl">Phone:</span> {application.contactPhone}</div>}
                {application.contactCell && <div><span className="lbl">Cell:</span> {application.contactCell}</div>}
              </div>
            </div>
          )}
        </section>

        <section className="permit-pay">
          <div className="pay-table">
            <span className="pay-a" />
            <span className="pay-b">Total Due:</span>
            <span className="pay-c">{money(totalDue)}</span>

            <span className="pay-a">Receipt Number: {payment?.receiptNum || '—'}</span>
            <span className="pay-b">Total Amount Paid:</span>
            <span className="pay-c pay-c--rule">{money(paid)}</span>

            <span className="pay-a">Payer Name: {payment?.acctName || ''}</span>
            <span className="pay-b">{paidByLabel}: {payment?.paymentType ? formatPaymentType(payment.paymentType) : '—'}</span>
            <span className={`pay-c pay-status${paidInFull ? ' pay-status--ok' : ''}`}>{payStatusLabel}</span>
          </div>
        </section>

        <section className="permit-works">
          <h2>Works Requesting Permits</h2>
          {works.map((work) => {
            const cancelled = String(work.statusCode || '').toUpperCase() === 'CAN';
            const wells = countWells(work);
            const workName = [
              work.workCategoryDesc || work.workCategory,
              work.workTypeDesc || work.workType,
              work.wellUseDesc || work.wellUseType,
            ].filter(Boolean).join(' - ');
            const isBorehole = ['inv', 'invprb'].includes(String(work.workCategory || '').toLowerCase());
            const driller = joinName(work.drillerName)
              + (work.drillerLicenseNum ? ` — Lic #: ${work.drillerLicenseNum}` : '')
              + ((work.drillMethodName || work.drillMethodType) ? ` — Method: ${work.drillMethodName || work.drillMethodType}` : '');
            const lines = permitsByWork[work.workId] || [];
            const lineByWorkOrSpec = (spec) => lines.find((l) => l.workSpecsId === spec.workSpecsId) || (lines.length ? lines[0] : null);
            const specs = work.specs || [];
            const hasStateWell = !isBorehole && specs.some((s) => s.stateWellId);
            const hasOrigPermit = !isBorehole && specs.some((s) => s.permitNum);
            const hasDwr = !isBorehole && specs.some((s) => s.dwrNum);

            return (
              <div className={`work-block${cancelled ? ' work-block--cancelled' : ''}`} key={work.workId}>
                <div className="work-head">
                  <span>{workName} — {wells} {isBorehole ? 'Boreholes' : 'Wells'}</span>
                  <span className="work-total">Work Total: {money(cancelled ? 0 : workCalcAmount(work))}</span>
                </div>
                <div className="muted work-driller">Driller: {driller || '—'}</div>
                {cancelled && <div className="work-cancelled-note">** Cancelled Work. Total amount adjusted. **</div>}
                <div className="work-specs-label">Specifications</div>
                <table className="work-specs">
                  <thead>
                    <tr>
                      <th>Permit #</th>
                      <th>Issued</th>
                      <th>Expires</th>
                      <th>{isBorehole ? '# Boreholes' : 'Owner Well Id'}</th>
                      <th>Hole Diam.</th>
                      {!isBorehole && <th>Casing Diam.</th>}
                      {!isBorehole && <th>Seal Depth</th>}
                      <th>Max. Depth</th>
                      {hasStateWell && <th>State Well #</th>}
                      {hasOrigPermit && <th>Orig. Permit #</th>}
                      {hasDwr && <th>DWR #</th>}
                    </tr>
                  </thead>
                  <tbody>
                    {specs.map((spec) => {
                      const line = lineByWorkOrSpec(spec);
                      const permitCell = line?.permitNumber
                        ? line.permitNumber
                        : (cancelled ? '* Cancelled *' : '* Pending Approval *');
                      return (
                        <tr key={spec.workSpecsId}>
                          <td className="nowrap">{permitCell}</td>
                          <td>{line ? fmtDate(line.issuedDate) : ''}</td>
                          <td>{line ? fmtDate(line.expireDate) : ''}</td>
                          <td>{isBorehole ? (spec.drillCount ?? '') : (spec.ownerWellNum || '')}</td>
                          <td>{spec.holeDiamIn != null ? `${spec.holeDiamIn} in.` : ''}</td>
                          {!isBorehole && <td>{spec.casingDiamIn != null ? `${spec.casingDiamIn} in.` : ''}</td>}
                          {!isBorehole && <td>{spec.sealDepthFt != null ? `${spec.sealDepthFt} ft` : ''}</td>}
                          <td>{spec.maxDepthFt != null ? `${spec.maxDepthFt} ft` : ''}</td>
                          {hasStateWell && <td>{spec.stateWellId || ''}</td>}
                          {hasOrigPermit && <td>{spec.permitNum || ''}</td>}
                          {hasDwr && <td>{spec.dwrNum || ''}</td>}
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            );
          })}
        </section>

        {selectedConditions.length > 0 && (
          <section className="permit-conditions">
            <h2>Specific Work Permit Conditions</h2>
            <ol>
              {selectedConditions.map((c) => <li key={c.idx}>{c.text}</li>)}
            </ol>
          </section>
        )}

        <div className="permit-rule" />
        <footer className="permit-foot">
          Alameda County Public Works Agency · Water Resources Section — Application {application.appId}
        </footer>
      </div>
    </main>
  );
}

export default PermitPage;
