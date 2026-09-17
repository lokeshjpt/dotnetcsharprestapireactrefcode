import FormSection from '../../../components/FormSection/FormSection';
import SelectField from '../../../components/UI/SelectField';
import MapPicker from '../../../components/MapPicker/MapPicker';

function StepLocation({ formData, updateField, setFields, options, errors = {} }) {
  const cities = options.cities && options.cities.length ? options.cities : [];

  const setLocation = (siteLat, siteLong) => setFields({ siteLat, siteLong });

  // Fired by the map when a place is searched or the map is clicked. Faithful to
  // legacy app_proj_locmap.jsp: the geocoded address populates the Location
  // Address/Description and the city is matched to a known Alameda County city.
  const handlePlace = ({ address, city, lat, lng }) => {
    const next = { siteLat: lat, siteLong: lng };
    if (address) next.siteLoc = address;
    if (city) {
      const match = cities.find((c) => c.label.toLowerCase() === city.toLowerCase());
      if (match) {
        next.siteCity = match.code;
        next.siteCityName = match.label;
      }
    }
    setFields(next);
  };

  const hasCoords = formData.siteLat && formData.siteLong;

  return (
    <FormSection title="Project Location" borderColor="m-blue">
      <p className="text-muted">
        Identify the project location on the map. Click on the map or enter an address/location in
        the &lsquo;Search Address&rsquo; box to position the marker. The address and place name
        populate the Location Address / Description field &mdash; add any additional description
        below (200 characters max).
      </p>

      <MapPicker
        idPrefix="site"
        lat={formData.siteLat}
        lng={formData.siteLong}
        onChange={setLocation}
        onPlace={handlePlace}
        showSearch
        showInputs={false}
      />

      <div className="form-section__body form-grid-2" style={{ marginTop: 'var(--spacing-md)' }}>
        <div className="span-2 form-row">
          <label className="form-label" htmlFor="siteLoc">
            Location Address / Description<span className="mandatory" aria-hidden="true">*</span>
            <span className="sr-only"> (required)</span>
          </label>
          <div className="form-col">
            <textarea
              id="siteLoc"
              className={`form-control${errors.siteLoc ? ' is-invalid' : ''}`}
              rows={3}
              maxLength={200}
              value={formData.siteLoc}
              onChange={(e) => updateField('siteLoc', e.target.value)}
              aria-invalid={errors.siteLoc ? true : undefined}
              aria-describedby={errors.siteLoc ? 'siteLoc-error' : undefined}
              aria-required="true"
            />
            {errors.siteLoc && <span id="siteLoc-error" className="field-error" role="alert">{errors.siteLoc}</span>}
          </div>
        </div>

        <SelectField
          id="siteCity"
          label="Location City"
          required
          placeholder="Select..."
          options={cities}
          value={formData.siteCity}
          onChange={(e) => {
            const code = e.target.value;
            const match = cities.find((c) => c.code === code);
            setFields({ siteCity: code, siteCityName: match ? match.label : '' });
          }}
          error={errors.siteCity}
        />

        <div className="form-row">
          <span className="form-label" id="latlng-label">Lat / Long</span>
          <div className="form-col" aria-labelledby="latlng-label">
            <span className="form-control-static">
              {hasCoords ? `(${formData.siteLat}, ${formData.siteLong})` : 'Not set — pick a location above'}
            </span>
            {errors.siteLat && <span className="field-error" role="alert">{errors.siteLat}</span>}
          </div>
        </div>
      </div>
    </FormSection>
  );
}

export default StepLocation;
