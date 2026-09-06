using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DentalClinic.Models
{
    public class UpdateAppointmentRequest
    {
        private DateTime? _appointmentDate;
        private string? _comment;
        private int? _doctorId;

        [StringLength(20)]
        public string? Status { get; set; }

        // Nullable update fields need to distinguish an omitted property (leave the
        // stored value unchanged) from an explicit JSON null (clear the stored value).
        // System.Text.Json invokes the setter only when the property is present, so
        // these internal presence flags preserve real PATCH-like semantics without
        // adding separate clear* fields to the public API contract.
        public DateTime? AppointmentDate
        {
            get => _appointmentDate;
            set
            {
                _appointmentDate = value;
                AppointmentDateSpecified = true;
            }
        }

        [JsonIgnore]
        public bool AppointmentDateSpecified { get; private set; }

        [StringLength(500)]
        public string? Comment
        {
            get => _comment;
            set
            {
                _comment = value;
                CommentSpecified = true;
            }
        }

        [JsonIgnore]
        public bool CommentSpecified { get; private set; }

        public int? DoctorId
        {
            get => _doctorId;
            set
            {
                _doctorId = value;
                DoctorIdSpecified = true;
            }
        }

        [JsonIgnore]
        public bool DoctorIdSpecified { get; private set; }
    }
}
