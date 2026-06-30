using System.ComponentModel.DataAnnotations;

namespace VitalBand.Models
{
    public class ResetPasswordViewModel
    {
        [Required]
        public string Token { get; set; }

        [Required]
        public string Email { get; set; }

        // 2. Los dos inputs que el médico o paciente van a llenar en la pantalla
        [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "Debes confirmar tu contraseña.")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Las contraseñas no coinciden. 🔒")]
        public string ConfirmPassword { get; set; }
    }
}
