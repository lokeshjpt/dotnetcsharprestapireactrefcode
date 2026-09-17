namespace PWA.PermitsApi.Application.DTOs;

public sealed record SubmitHazardRequest
{
    public bool Present { get; init; }

    public string? ConsultantFirstName { get; init; }
    public string? ConsultantLastName { get; init; }
    public string? ConsultantPhone { get; init; }
    public string? ConsultantCell { get; init; }

    public string? SafetyOfficerFirstName { get; init; }
    public string? SafetyOfficerLastName { get; init; }
    public string? SafetyOfficerPhone { get; init; }
    public string? SafetyOfficerCell { get; init; }

    public string? FacilityType { get; init; }

    public DateTime? SiteSafetyMeetingDate { get; init; }
    public string? SiteSafetyMeetingTime { get; init; }

    // PPE levels (Y/N)
    public string? PpeLevelA { get; init; }
    public string? PpeLevelB { get; init; }
    public string? PpeLevelC { get; init; }
    public string? PpeLevelD { get; init; }

    // Equipment flags (R = required, A = available) + descriptions
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

    // Info provided by + acknowledgement
    public string? InfoProvidedByCompanyName { get; init; }
    public string? InfoProvidedByLastName { get; init; }
    public string? InfoProvidedByFirstName { get; init; }
    public string? InfoProvidedByTitle { get; init; }
    public string? InfoProvidedByPhone { get; init; }
    public bool Acknowledgement { get; init; }

    public IReadOnlyList<string> Contaminants { get; init; } = new List<string>();
    public IReadOnlyList<SubmitHazardSubstanceRequest> Substances { get; init; } = new List<SubmitHazardSubstanceRequest>();
}
