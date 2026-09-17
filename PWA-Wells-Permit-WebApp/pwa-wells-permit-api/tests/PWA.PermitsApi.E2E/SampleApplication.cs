namespace PWA.PermitsApi.E2E;

/// <summary>
/// Builds a representative EXMPT (fee-exempt, $0) application payload that
/// includes a fully populated site-hazard sub-form, mirroring the shape the
/// React ecomm UI posts to POST /api/applications.
/// </summary>
public static class SampleApplication
{
    public const string ClothingDesc = "Tyvek chemical-resistant suit";
    public const string InfoProvidedByLastName = "Rivera";

    public static object BuildExemptWithHazard()
    {
        return new
        {
            addBy = "e2e-playwright",

            // Applicant
            appBusinessName = "Bayside Drilling Co.",
            appLastName = "Nguyen",
            appFirstName = "Ana",
            appEmailAddr = "ana.nguyen@example.com",
            appAddrStreet = "1401 Lakeside Dr",
            appAddrCity = "Oakland",
            appAddrState = "CA",
            appAddrZip = "94612",
            appPhone = "510-555-0100",

            // Property owner
            ownerLastName = "Okafor",
            ownerFirstName = "Ben",
            ownerAddrStreet = "88 Harbor Bay Pkwy",
            ownerAddrCity = "Oakland",
            ownerAddrState = "CA",
            ownerAddrZip = "94502",

            // Project / site
            siteCityCode = "OAK",
            siteCityName = "Oakland",
            siteLocation = "NW corner of 5th St and Broadway",
            projStartDate = "2026-09-01",
            projEndDate = "2026-09-30",
            siteHazardRequired = "Y",

            // One work line item
            works = new[]
            {
                new
                {
                    workCategory = "con",
                    workType = "cathodic",
                    workFeeRate = 660.00m,
                    workFeeUnit = "well",
                },
            },

            // Hazardous-materials sub-form
            hazard = new
            {
                present = true,
                ppeLevelA = "Y",
                equipHardHatFlag = "R",
                equipClothingFlag = "R",
                equipClothingDesc = ClothingDesc,
                infoProvidedByLastName = InfoProvidedByLastName,
                acknowledgement = true,
            },

            // Payment
            paymentType = "EXMPT",
        };
    }
}
