import { useEffect, useState } from 'react';
import FormSection from '../../../components/FormSection/FormSection';
import SelectField from '../../../components/UI/SelectField';
import { getWorkTypes } from '../../../api/referenceApi';
import { WORK_CATEGORIES, WORK_TYPES, formatDollar } from '../referenceData';

function normalizeType(t) {
  return {
    code: t.code ?? t.workType ?? t.work_type,
    label: t.label ?? t.workDesc ?? t.work_desc,
    feeRate: t.feeRate ?? t.fee_rate_amt ?? t.feeRateAmt ?? 0,
    feeUnit: t.feeUnit ?? t.fee_unit ?? 'EA',
    siteMax: t.siteMax ?? t.site_max ?? 0,
    siteExtraRate: t.siteExtraRate ?? t.site_extra_rate ?? t.workSiteExtraRate ?? 0,
  };
}

function Step3WorkType({ formData, updateField, setFields, options, errors = {} }) {
  const categories = options.workCategories && options.workCategories.length ? options.workCategories : WORK_CATEGORIES;
  const [types, setTypes] = useState([]);

  useEffect(() => {
    let active = true;
    const cat = formData.workCat;
    if (!cat) {
      setTypes([]);
      return undefined;
    }
    async function load() {
      let list = [];
      try {
        const data = await getWorkTypes(cat);
        list = Array.isArray(data) && data.length ? data : (WORK_TYPES[cat] || []);
      } catch {
        list = WORK_TYPES[cat] || [];
      }
      if (active) setTypes(list.map(normalizeType));
    }
    load();
    return () => {
      active = false;
    };
  }, [formData.workCat]);

  const onCategoryChange = (e) => {
    setFields({ workCat: e.target.value, workType: '', workDesc: '', workFeeRate: 0, workFeeUnit: 'EA', workSiteMax: 0, workSiteExtraRate: 0 });
  };

  const onTypeChange = (e) => {
    const selected = types.find((t) => t.code === e.target.value);
    if (selected) {
      setFields({
        workType: selected.code,
        workDesc: selected.label,
        workFeeRate: selected.feeRate,
        workFeeUnit: selected.feeUnit,
        workSiteMax: selected.siteMax,
        workSiteExtraRate: selected.siteExtraRate,
      });
    } else {
      updateField('workType', e.target.value);
    }
  };

  const typeOptions = types.map((t) => ({
    code: t.code,
    label: `${t.label} - ${formatDollar(t.feeRate)} per ${t.feeUnit}`,
  }));

  return (
    <FormSection title="Identify Type of Work" borderColor="m-blue" columns={2}>
      <SelectField
        id="workCat"
        label="Work Category"
        required
        placeholder="Select a category..."
        options={categories}
        value={formData.workCat}
        onChange={onCategoryChange}
        error={errors.workCat}
      />
      <SelectField
        id="workType"
        label="Work Type"
        required
        placeholder={formData.workCat ? 'Select a work type...' : 'Select a category first'}
        options={typeOptions}
        value={formData.workType}
        onChange={onTypeChange}
        disabled={!formData.workCat}
        error={errors.workType}
      />
      {formData.workType && (
        <div className="form-row span-2">
          <label className="form-label">Fee</label>
          <div className="form-col">
            <strong>{formatDollar(formData.workFeeRate)}</strong> per {formData.workFeeUnit}
          </div>
        </div>
      )}
    </FormSection>
  );
}

export default Step3WorkType;