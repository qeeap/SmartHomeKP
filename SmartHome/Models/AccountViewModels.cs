using System.ComponentModel.DataAnnotations;
using SmartHome.Domain.Enums;

namespace SmartHome.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Введите логин")]
        public string Login { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите пароль")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите Email")]
        [EmailAddress(ErrorMessage = "Некорректный Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите ФИО")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Выберите роль")]
        public UserRole Role { get; set; }
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Введите логин")]
        public string Login { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите пароль")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}