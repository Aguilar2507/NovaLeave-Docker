namespace NovaLeave.Infrastructure.Identity;

// Contract over the development seeder so DatabaseInitializer can be tested without
// provisioning Identity users. Seeding creates accounts with known passwords, so the
// test that proves it never runs outside Development is security-relevant, not cosmetic.
public interface IDevelopmentDataSeeder
{
    Task SeedAsync();
}
