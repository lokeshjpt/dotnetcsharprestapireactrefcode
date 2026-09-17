namespace PWA.PermitsApi.Application.DTOs;

/// <summary>
/// Staff edit of the full "Site Hazard Information" packet — the legacy four-step flow
/// (update_hazard_info / _equipment / _subs / _provider) collapsed into one request that
/// writes APP_HAZARD_INFO plus the APP_SITE_CONTAMINATIONS and APP_HAZARD_SUBSTANCE child rows.
/// </summary>
public sealed record UpdateHazardRequest
{
    // Site Hazard Information (update_hazard_info.jsp)
    public string? ConsultantFirstName { get; init; }
    public string? ConsultantLastName { get; init; }
    public string? ConsultantPhone { get; init; }
    public string? ConsultantCell { get; init; }

    public string? SafetyOfficerFirstName { get; init; }
    public string? SafetyOfficerLastName { get; init; }
    public string? SafetyOfficerPhone { get; init; }
    public string? SafetyOfficerCell { get; init; }

    public string? FacilityType { get; init; }
    public DateTime? SiteSafetyMeetingDateTime { get; init; }

    // Personal Protective Equipment (update_hazard_equipment.jsp)
    // Levels are "Y"/""; equipment item flags are "R"/"A"/"".
    public string? PpeLevelA { get; init; }
    public string? PpeLevelB { get; init; }
    public string? PpeLevelC { get; init; }
    public string? PpeLevelD { get; init; }

    public string? EquipHardHatFlag { get; init; }
    public string? EquipSafetyShoesFlag { get; init; }
    public string? EquipOrangeVestFlag { get; init; }
    public string? EquipHearingProtFlag { get; init; }
    public string? EquipSafetyEyewearFlag { get; init; }
    public string? EquipClothingFlag { get; init; }
    public string? EquipClothingDesc { get; init; }
    public string? EquipRespiratorFlag { get; init; }
    public string? EquipRespiratorDesc { get; init; }
    public string? EquipCartridgeFlag { get; init; }
    public string? EquipCartridgeDesc { get; init; }
    public string? EquipGlovesFlag { get; init; }
    public string? EquipGlovesDesc { get; init; }
    public string? EquipOtherFlag { get; init; }
    public string? EquipOtherDesc { get; init; }

    // Anticipated Hazardous Substances (update_hazard_subs.jsp)
    public IReadOnlyList<string> Contaminants { get; init; } = new List<string>();
    public IReadOnlyList<HazardSubstanceDto> Substances { get; init; } = new List<HazardSubstanceDto>();

    // Site Hazard Provider (update_hazard_provider.jsp)
    public string? InfoProvidedByFirstName { get; init; }
    public string? InfoProvidedByLastName { get; init; }
    public string? InfoProvidedByTitle { get; init; }
    public string? InfoProvidedByPhone { get; init; }
}
