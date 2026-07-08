using System.Text.Json;
using HSCSAPI.Models.Enums;

namespace HSCSAPI.SeedData;

public static class LabTestTemplateSeedData
{
    public static IReadOnlyList<LabTemplateSeed> Templates { get; } =
    [
        new(
            "CBC-DIFF",
            "Complete Blood Count with Differential",
            "57021-8",
            "EDTA whole blood",
            "No fasting is usually required. Follow the laboratory collection protocol.",
            "https://loinc.org/57021-8/",
            [
                N("wbc", "White blood cells", "6690-2", "10^3/uL", 2),
                N("rbc", "Red blood cells", "789-8", "10^6/uL", 2),
                N("hemoglobin", "Hemoglobin", "718-7", "g/dL", 1),
                N("hematocrit", "Hematocrit", "4544-3", "%", 1),
                N("mcv", "Mean corpuscular volume", "787-2", "fL", 1),
                N("mch", "Mean corpuscular hemoglobin", "785-6", "pg", 1),
                N("mchc", "Mean corpuscular hemoglobin concentration", "786-4", "g/dL", 1),
                N("rdw", "Red cell distribution width", "788-0", "%", 1),
                N("platelets", "Platelets", "777-3", "10^3/uL", 0),
                N("neutrophils_percent", "Neutrophils", "770-8", "%", 1),
                N("lymphocytes_percent", "Lymphocytes", "736-9", "%", 1),
                N("monocytes_percent", "Monocytes", "5905-5", "%", 1),
                N("eosinophils_percent", "Eosinophils", "713-8", "%", 1),
                N("basophils_percent", "Basophils", "706-2", "%", 1)
            ]),
        new(
            "CMP",
            "Comprehensive Metabolic Panel",
            "24323-8",
            "Serum or plasma",
            "Fasting may be requested by the ordering clinician or laboratory.",
            "https://loinc.org/24323-8/",
            [
                N("glucose", "Glucose", "2345-7", "mg/dL", 1),
                N("calcium", "Calcium", "17861-6", "mg/dL", 1),
                N("sodium", "Sodium", "2951-2", "mmol/L", 1),
                N("potassium", "Potassium", "2823-3", "mmol/L", 1),
                N("chloride", "Chloride", "2075-0", "mmol/L", 1),
                N("co2", "Carbon dioxide", "2028-9", "mmol/L", 1),
                N("bun", "Blood urea nitrogen", "3094-0", "mg/dL", 1),
                N("creatinine", "Creatinine", "2160-0", "mg/dL", 2),
                N("total_protein", "Total protein", "2885-2", "g/dL", 1),
                N("albumin", "Albumin", "1751-7", "g/dL", 1),
                N("total_bilirubin", "Total bilirubin", "1975-2", "mg/dL", 2),
                N("alp", "Alkaline phosphatase", "6768-6", "U/L", 0),
                N("ast", "Aspartate aminotransferase", "1920-8", "U/L", 0),
                N("alt", "Alanine aminotransferase", "1742-6", "U/L", 0)
            ]),
        new(
            "URINALYSIS",
            "Complete Urinalysis",
            "24356-8",
            "Fresh clean-catch urine",
            "Use a clean-catch specimen and deliver it according to laboratory timing requirements.",
            "https://loinc.org/24356-8/",
            [
                C("color", "Color", "5778-6", ["Pale Yellow", "Yellow", "Amber", "Red", "Brown", "Other"]),
                C("appearance", "Appearance", "5767-9", ["Clear", "Slightly Cloudy", "Cloudy", "Turbid"]),
                N("specific_gravity", "Specific gravity", "5811-5", null, 3),
                N("ph", "pH", "5803-2", null, 1),
                C("protein", "Protein", "5804-0", ["Negative", "Trace", "1+", "2+", "3+", "4+"]),
                C("glucose", "Glucose", "5792-7", ["Negative", "Trace", "1+", "2+", "3+", "4+"]),
                C("ketones", "Ketones", "5797-6", ["Negative", "Trace", "Small", "Moderate", "Large"]),
                C("blood", "Blood", "5794-3", ["Negative", "Trace", "1+", "2+", "3+"]),
                C("bilirubin", "Bilirubin", "5770-3", ["Negative", "1+", "2+", "3+"]),
                N("urobilinogen", "Urobilinogen", "5818-0", "mg/dL", 1),
                C("nitrite", "Nitrite", "5802-4", ["Negative", "Positive"]),
                C("leukocyte_esterase", "Leukocyte esterase", "5799-2", ["Negative", "Trace", "1+", "2+", "3+"]),
                N("wbc_hpf", "White blood cells", "5821-4", "/HPF", 0),
                N("rbc_hpf", "Red blood cells", "5808-1", "/HPF", 0),
                C("epithelial_cells", "Epithelial cells", "11277-1", ["None", "Rare", "Few", "Moderate", "Many"]),
                C("bacteria", "Bacteria", "5769-5", ["None", "Rare", "Few", "Moderate", "Many"]),
                T("casts", "Casts", null, false),
                T("crystals", "Crystals", null, false)
            ]),
        new(
            "LIPID-DIRECT-LDL",
            "Lipid Panel with Direct LDL",
            "57698-3",
            "Serum or plasma",
            "Fasting requirements depend on the laboratory and ordering clinician.",
            "https://loinc.org/57698-3/",
            [
                N("total_cholesterol", "Total cholesterol", "2093-3", "mg/dL", 0),
                N("hdl", "HDL cholesterol", "2085-9", "mg/dL", 0),
                N("ldl_direct", "Direct LDL cholesterol", "18262-6", "mg/dL", 0),
                N("triglycerides", "Triglycerides", "2571-8", "mg/dL", 0)
            ]),
        new(
            "HBA1C",
            "Hemoglobin A1c",
            "4548-4",
            "EDTA whole blood",
            "No fasting is usually required.",
            "https://loinc.org/4548-4/",
            [N("hba1c", "Hemoglobin A1c", "4548-4", "%", 1)]),
        new(
            "THYROID-FT4-TSH",
            "Free T4 and TSH Panel",
            "24348-5",
            "Serum or plasma",
            "Record medications or supplements that may affect thyroid testing.",
            "https://loinc.org/24348-5/",
            [
                N("tsh", "Thyroid stimulating hormone", "3016-3", "mIU/L", 3),
                N("free_t4", "Free thyroxine", "3024-7", "ng/dL", 2)
            ]),
        new(
            "COAG-BASIC",
            "Basic Coagulation Panel",
            null,
            "Citrated plasma",
            "Use the required citrate tube fill volume and record anticoagulant therapy.",
            "https://medlineplus.gov/lab-tests/prothrombin-time-test-and-inr-ptinr/",
            [
                N("pt", "Prothrombin time", "5902-2", "s", 1),
                N("inr", "International normalized ratio", "6301-6", "ratio", 2),
                N("aptt", "Activated partial thromboplastin time", "14979-9", "s", 1),
                N("fibrinogen", "Fibrinogen", "3255-7", "mg/dL", 0, false)
            ]),
        new(
            "IRON-STUDIES",
            "Iron and Iron Binding Capacity Panel",
            "50190-8",
            "Serum or plasma",
            "Morning collection or fasting may be requested by the laboratory.",
            "https://loinc.org/50190-8/",
            [
                N("serum_iron", "Serum iron", "2498-4", "ug/dL", 0),
                N("tibc", "Total iron-binding capacity", "2500-7", "ug/dL", 0),
                N("transferrin", "Transferrin", "3034-6", "mg/dL", 0, false),
                N("transferrin_saturation", "Transferrin saturation", "2502-3", "%", 1),
                N("ferritin", "Ferritin", "2276-4", "ng/mL", 1)
            ]),
        new(
            "VITAMIN-D-25OH",
            "25-Hydroxy Vitamin D",
            "62292-8",
            "Serum or plasma",
            "No special preparation is usually required unless directed by the laboratory.",
            "https://loinc.org/62292-8/",
            [N("vitamin_d_25oh", "25-Hydroxy vitamin D", "62292-8", "ng/mL", 1)]),
        new(
            "STOOL-ANALYSIS",
            "Stool Analysis",
            null,
            "Fresh stool",
            "Avoid contamination with urine or water and follow the collection kit instructions.",
            "https://medlineplus.gov/lab-tests/ova-and-parasite-test/",
            [
                C("color", "Color", null, ["Brown", "Yellow", "Green", "Black", "Red", "Clay", "Other"]),
                C("consistency", "Consistency", null, ["Formed", "Soft", "Loose", "Watery", "Hard"]),
                C("occult_blood", "Occult blood", "57905-2", ["Negative", "Positive"]),
                N("wbc_hpf", "White blood cells", null, "/HPF", 0),
                N("rbc_hpf", "Red blood cells", null, "/HPF", 0),
                T("ova_parasites", "Ova and parasites", null),
                C("mucus", "Mucus", null, ["Absent", "Present"]),
                C("undigested_fibers", "Undigested food or fibers", null, ["None", "Rare", "Few", "Moderate", "Many"], false)
            ])
    ];

    private static LabFieldSeed N(
        string code,
        string label,
        string? loincCode,
        string? unit,
        int decimalPlaces,
        bool required = true) =>
        new(code, label, loincCode, LabResultValueType.Numeric, unit, required, decimalPlaces, ReferenceRangeFor(code), null);

    private static string? ReferenceRangeFor(string code)
    {
        return code.ToLowerInvariant() switch
        {
            "wbc" => "4.0-11.0",
            "rbc" => "4.2-5.9",
            "hemoglobin" => "12.0-17.5",
            "hematocrit" => "36-52",
            "mcv" => "80-100",
            "mch" => "27-33",
            "mchc" => "32-36",
            "rdw" => "11.5-14.5",
            "platelets" => "150-450",
            "neutrophils_percent" => "40-70",
            "lymphocytes_percent" => "20-45",
            "monocytes_percent" => "2-10",
            "eosinophils_percent" => "1-6",
            "basophils_percent" => "0-2",
            "glucose" => "70-99",
            "calcium" => "8.6-10.2",
            "sodium" => "135-145",
            "potassium" => "3.5-5.1",
            "chloride" => "98-107",
            "co2" => "22-29",
            "bun" => "7-20",
            "creatinine" => "0.6-1.3",
            "total_protein" => "6.0-8.3",
            "albumin" => "3.5-5.0",
            "total_bilirubin" => "0.1-1.2",
            "alp" => "44-147",
            "ast" => "10-40",
            "alt" => "7-56",
            "specific_gravity" => "1.005-1.030",
            "ph" => "4.5-8.0",
            "urobilinogen" => "0.2-1.0",
            "wbc_hpf" => "0-5",
            "rbc_hpf" => "0-2",
            "total_cholesterol" => "<200",
            "hdl" => ">=40",
            "ldl_direct" => "<100",
            "triglycerides" => "<150",
            "hba1c" => "4.0-5.6",
            "tsh" => "0.4-4.0",
            "free_t4" => "0.8-1.8",
            "pt" => "11-13.5",
            "inr" => "0.8-1.1",
            "aptt" => "25-35",
            "fibrinogen" => "200-400",
            "serum_iron" => "60-170",
            "tibc" => "240-450",
            "transferrin" => "200-360",
            "transferrin_saturation" => "20-50",
            "ferritin" => "30-400",
            "vitamin_d_25oh" => "30-100",
            _ => null
        };
    }

    private static LabFieldSeed T(string code, string label, string? loincCode, bool required = true) =>
        new(code, label, loincCode, LabResultValueType.Text, null, required, null, null, null);

    private static LabFieldSeed C(
        string code,
        string label,
        string? loincCode,
        IReadOnlyList<string> allowedValues,
        bool required = true) =>
        new(
            code,
            label,
            loincCode,
            LabResultValueType.Choice,
            null,
            required,
            null,
            null,
            JsonSerializer.Serialize(allowedValues));
}

public sealed record LabTemplateSeed(
    string Code,
    string Name,
    string? LoincCode,
    string SpecimenType,
    string? PreparationInstructions,
    string SourceUrl,
    IReadOnlyList<LabFieldSeed> Fields,
    int Version = 1);

public sealed record LabFieldSeed(
    string Code,
    string Label,
    string? LoincCode,
    LabResultValueType ValueType,
    string? Unit,
    bool IsRequired,
    int? DecimalPlaces,
    string? ReferenceRange,
    string? AllowedValuesJson);
