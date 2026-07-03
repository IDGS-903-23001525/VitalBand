using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
using VitalBand.Models;

namespace VitalBand.Data
{
    public class VitalBandContext : DbContext
    {
        public VitalBandContext(
            DbContextOptions<VitalBandContext> options)
            : base(options)
        {
        }

        // Mapeo de las tablas de la base de datos VitalBand
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Medico> Medicos { get; set; }
        public DbSet<Paciente> Pacientes { get; set; }
        public DbSet<Alerta> Alertas { get; set; }
        public DbSet<ContactoEmergencia> ContactosEmergencia { get; set; }
        public DbSet<PacientePatologia> PacientesPatologias { get; set; }
        public DbSet<PatologiaCatalogo> PatologiasCatalogo { get; set; }
    }
}