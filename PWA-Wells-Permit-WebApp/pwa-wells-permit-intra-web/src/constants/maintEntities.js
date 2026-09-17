// Metadata that drives the config-driven Code Maintenance screens (parity with the legacy intra
// maint_code_menu.jsp + maint_*_list.jsp). Each entity describes its list columns and its add/edit
// form fields; the generic MaintenanceEntity page renders the grid + modal from this.
//
// field.type: 'text' | 'textarea' | 'number' | 'flag' (Y/N checkbox) | 'date' (MM/DD/YYYY)
// column.type: 'text' | 'number' | 'active' (Y->Active) | 'yn' (Y->Yes) | 'datetime'

const ACTIVE_FIELD = { name: 'activeFlag', label: 'Active', type: 'flag', default: 'Y' };

export const MAINT_GROUPS = [
  {
    title: 'Location Codes',
    entities: ['cities', 'states'],
  },
  {
    title: 'Work Setup',
    entities: ['work-categories', 'work-types', 'well-use-types', 'drill-methods'],
  },
  {
    title: 'Conditions',
    entities: ['condition-types', 'work-condition-types'],
  },
  {
    title: 'Reference Codes',
    entities: ['payment-types', 'status-codes', 'document-types'],
  },
  {
    title: 'Inspections',
    entities: ['inspectors', 'inspection-unavailable-days', 'inspection-slots'],
  },
];

export const MAINT_ENTITIES = {
  cities: {
    key: 'cities',
    endpoint: 'cities',
    title: 'City Codes',
    subtitle: 'City lookup codes and county jurisdiction.',
    rowKey: (r) => r.cityCode,
    columns: [
      { field: 'cityCode', label: 'Code' },
      { field: 'cityName', label: 'City Name' },
      { field: 'countyJuris', label: 'Jurisdiction' },
    ],
    fields: [
      { name: 'cityCode', label: 'City Code', type: 'text', required: true, keyField: true },
      { name: 'cityName', label: 'City Name', type: 'text', required: true },
      { name: 'countyJuris', label: 'County Jurisdiction', type: 'text' },
    ],
    delete: { type: 'path', field: 'cityCode' },
  },

  states: {
    key: 'states',
    endpoint: 'states',
    title: 'State Codes',
    subtitle: 'US state / territory codes.',
    rowKey: (r) => r.stateCode,
    columns: [
      { field: 'stateCode', label: 'State Code' },
      { field: 'stateName', label: 'State Name' },
    ],
    fields: [
      { name: 'stateCode', label: 'State Code', type: 'text', required: true, keyField: true, maxLength: 2 },
      { name: 'stateName', label: 'State Name', type: 'text', required: true },
    ],
    delete: { type: 'path', field: 'stateCode' },
  },

  'work-categories': {
    key: 'work-categories',
    endpoint: 'work-categories',
    title: 'Work Categories',
    subtitle: 'Top-level work category codes.',
    rowKey: (r) => r.workCategory,
    columns: [
      { field: 'workCategory', label: 'Work Category' },
      { field: 'workCatDesc', label: 'Work Category Description' },
      { field: 'activeFlag', label: 'Active', type: 'active' },
    ],
    fields: [
      { name: 'workCategory', label: 'Work Category', type: 'text', required: true, keyField: true },
      { name: 'workCatDesc', label: 'Description', type: 'text', required: true },
      ACTIVE_FIELD,
    ],
    delete: { type: 'path', field: 'workCategory' },
  },

  'work-types': {
    key: 'work-types',
    endpoint: 'work-types',
    title: 'Work Types',
    subtitle: 'Work types with fee rate, unit, and DWR / Geolog requirements.',
    rowKey: (r) => `${r.workCategory}|${r.workType}`,
    columns: [
      { field: 'workCatDesc', label: 'Work Category' },
      { field: 'workType', label: 'Work Type' },
      { field: 'workDesc', label: 'Work Type Description' },
      { field: 'feeRateAmt', label: 'Fee Rate Amount', type: 'money' },
      { field: 'feeUnit', label: 'Fee Unit' },
      { field: 'siteMax', label: 'Site Max', type: 'number' },
      { field: 'dwrRequired', label: 'DWR Required', type: 'yn' },
      { field: 'geologRequired', label: 'Geolog Required', type: 'yn' },
      { field: 'activeFlag', label: 'Active', type: 'active' },
    ],
    fields: [
      { name: 'workCategory', label: 'Work Category', type: 'text', required: true, keyField: true },
      { name: 'workType', label: 'Work Type', type: 'text', required: true, keyField: true },
      { name: 'workDesc', label: 'Description', type: 'text', required: true },
      { name: 'feeRateAmt', label: 'Fee Rate Amount', type: 'number', required: true, step: '0.01' },
      { name: 'feeUnit', label: 'Fee Unit', type: 'text', required: true },
      { name: 'siteMax', label: 'Site Max', type: 'number' },
      { name: 'dwrRequired', label: 'DWR Required', type: 'flag', default: 'N' },
      { name: 'geologRequired', label: 'Geolog Required', type: 'flag', default: 'N' },
      ACTIVE_FIELD,
    ],
    delete: { type: 'query', params: { cat: 'workCategory', type: 'workType' } },
  },

  'well-use-types': {
    key: 'well-use-types',
    endpoint: 'well-use-types',
    title: 'Well Use Types',
    subtitle: 'Well use types scoped to a work category and type.',
    rowKey: (r) => `${r.workCategory}|${r.workType}|${r.wellUseType}`,
    columns: [
      { field: 'workCatDesc', label: 'Work Category' },
      { field: 'workDesc', label: 'Work Type' },
      { field: 'wellUseType', label: 'Well Use Type' },
      { field: 'wellUseDesc', label: 'Well Use Description' },
      { field: 'activeFlag', label: 'Active', type: 'active' },
    ],
    fields: [
      { name: 'workCategory', label: 'Work Category', type: 'text', required: true, keyField: true },
      { name: 'workType', label: 'Work Type', type: 'text', required: true, keyField: true },
      { name: 'wellUseType', label: 'Well Use Type', type: 'text', required: true, keyField: true },
      { name: 'wellUseDesc', label: 'Description', type: 'text', required: true },
      ACTIVE_FIELD,
    ],
    delete: { type: 'query', params: { cat: 'workCategory', type: 'workType', use: 'wellUseType' } },
  },

  'drill-methods': {
    key: 'drill-methods',
    endpoint: 'drill-methods',
    title: 'Drill Method Types',
    subtitle: 'Drilling method codes.',
    rowKey: (r) => r.drillMethodType,
    columns: [
      { field: 'drillMethodType', label: 'Drill Type' },
      { field: 'drillMethodName', label: 'Drill Method' },
      { field: 'activeFlag', label: 'Active', type: 'active' },
    ],
    fields: [
      { name: 'drillMethodType', label: 'Drill Type', type: 'text', required: true, keyField: true },
      { name: 'drillMethodName', label: 'Method Name', type: 'text', required: true },
      ACTIVE_FIELD,
    ],
    delete: { type: 'path', field: 'drillMethodType' },
  },

  'condition-types': {
    key: 'condition-types',
    endpoint: 'condition-types',
    title: 'Condition Types',
    subtitle: 'Permit condition codes and descriptions.',
    rowKey: (r) => r.conditionType,
    columns: [
      { field: 'conditionType', label: 'Condition Type' },
      { field: 'conditionDesc', label: 'Conditions Desc' },
      { field: 'activeFlag', label: 'Active', type: 'active' },
    ],
    fields: [
      { name: 'conditionType', label: 'Condition Type', type: 'text', required: true, keyField: true },
      { name: 'conditionDesc', label: 'Description', type: 'textarea', required: true },
      ACTIVE_FIELD,
    ],
    delete: { type: 'path', field: 'conditionType' },
  },

  'work-condition-types': {
    key: 'work-condition-types',
    endpoint: 'work-condition-types',
    title: 'Work Condition Types',
    subtitle: 'Links a condition type to a work category and type (add / delete only).',
    rowKey: (r) => `${r.workCategory}|${r.workType}|${r.conditionType}`,
    noEdit: true,
    columns: [
      { field: 'workCatDesc', label: 'Work Category' },
      { field: 'workDesc', label: 'Work Type' },
      { field: 'conditionDesc', label: 'Condition Type' },
    ],
    fields: [
      { name: 'workCategory', label: 'Work Category', type: 'text', required: true, keyField: true },
      { name: 'workType', label: 'Work Type', type: 'text', required: true, keyField: true },
      { name: 'conditionType', label: 'Condition Type', type: 'text', required: true, keyField: true },
    ],
    delete: { type: 'query', params: { cat: 'workCategory', type: 'workType', cond: 'conditionType' } },
  },

  'payment-types': {
    key: 'payment-types',
    endpoint: 'payment-types',
    title: 'Payment Types',
    subtitle: 'Payment method codes, service charge, and display order.',
    rowKey: (r) => r.paymentType,
    columns: [
      { field: 'paymentType', label: 'Payment Type' },
      { field: 'paymentDesc', label: 'Payment Description' },
      { field: 'serviceCharge', label: 'Service Charge', type: 'money' },
      { field: 'displaySeq', label: 'Display Sequence', type: 'number' },
    ],
    fields: [
      { name: 'paymentType', label: 'Payment Type', type: 'text', required: true, keyField: true },
      { name: 'paymentDesc', label: 'Description', type: 'text', required: true },
      { name: 'serviceCharge', label: 'Service Charge', type: 'number', step: '0.01' },
      { name: 'displaySeq', label: 'Display Sequence', type: 'number', required: true },
    ],
    delete: { type: 'path', field: 'paymentType' },
  },

  'status-codes': {
    key: 'status-codes',
    endpoint: 'status-codes',
    title: 'Status Codes',
    subtitle: 'Application / work status codes and sort order.',
    rowKey: (r) => r.statusCode,
    columns: [
      { field: 'statusCode', label: 'Status Code' },
      { field: 'statusDesc', label: 'Status Description' },
      { field: 'sortSeq', label: 'Display Sequence', type: 'number' },
    ],
    fields: [
      { name: 'statusCode', label: 'Status Code', type: 'text', required: true, keyField: true },
      { name: 'statusDesc', label: 'Description', type: 'text', required: true },
      { name: 'sortSeq', label: 'Display Sequence', type: 'number', required: true },
    ],
    delete: { type: 'path', field: 'statusCode' },
  },

  'document-types': {
    key: 'document-types',
    endpoint: 'document-types',
    title: 'Document Types',
    subtitle: 'Uploaded document type codes.',
    rowKey: (r) => r.documentType,
    columns: [
      { field: 'documentType', label: 'Document Type' },
      { field: 'documentDesc', label: 'Document Desc' },
    ],
    fields: [
      { name: 'documentType', label: 'Document Type', type: 'text', required: true, keyField: true, maxLength: 20 },
      { name: 'documentDesc', label: 'Description', type: 'text', required: true },
    ],
    delete: { type: 'path', field: 'documentType' },
  },

  inspectors: {
    key: 'inspectors',
    endpoint: 'inspectors',
    title: 'Inspectors',
    subtitle: 'Field inspectors (add / edit — inspectors are deactivated, not deleted).',
    rowKey: (r) => r.inspectorId,
    noDelete: true,
    columns: [
      { field: 'inspectorName', label: 'Inspector Name' },
      { field: 'inspectorEmail', label: 'Email Address' },
      { field: 'inspectorPhone', label: 'Phone' },
      { field: 'activeFlag', label: 'Active Flag', type: 'active' },
    ],
    fields: [
      { name: 'inspectorId', label: 'Inspector ID', type: 'number', keyField: true, hideOnAdd: true, readOnly: true },
      { name: 'inspectorName', label: 'Name', type: 'text', required: true },
      { name: 'inspectorPhone', label: 'Phone', type: 'text', required: true },
      { name: 'inspectorEmail', label: 'Email', type: 'text' },
      ACTIVE_FIELD,
    ],
  },

  'inspection-unavailable-days': {
    key: 'inspection-unavailable-days',
    endpoint: 'inspection-unavailable-days',
    title: 'Inspection Unavailable Days',
    subtitle: 'Dates blocked from inspection scheduling.',
    rowKey: (r) => r.inspectionDate,
    columns: [
      { field: 'inspectionDate', label: 'Date' },
      { field: 'comments', label: 'Comments' },
    ],
    fields: [
      { name: 'inspectionDate', label: 'Inspection Date', type: 'date', required: true, keyField: true },
      { name: 'comments', label: 'Comments', type: 'text', required: true },
    ],
    delete: { type: 'query', params: { date: 'inspectionDate' } },
  },

  'inspection-slots': {
    key: 'inspection-slots',
    endpoint: 'inspection-slots',
    title: 'Inspection Max Slots Per Day',
    subtitle: 'Maximum number of inspection appointments allowed per day.',
    singleton: true,
  },
};

export function findMaintEntity(key) {
  return MAINT_ENTITIES[key] || null;
}
