using NUnit.Framework;
using Npgsql;
using SWE.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using System.Net.Sockets;

[TestFixture]
public class PackageTests
{
    private const string ConnectionString = "Host=localhost;Username=postgres;Password=fhtw;Database=mtcg_test;Port=5432";
    private NpgsqlConnection _connection;

    [SetUp]
    public void SetUp()
    {
        _connection = new NpgsqlConnection(ConnectionString);
        _connection.Open();

        CleanUpDatabase();
    }

    [TearDown]
    public void TearDown()
    {
        _connection.Close();
    }

    private void CleanUpDatabase()
    {
        using (var cmd = new NpgsqlCommand("DELETE FROM package_cards; DELETE FROM cards; DELETE FROM packages;", _connection))
        {
            cmd.ExecuteNonQuery();
        }
    }

    [Test]
    public async Task CreatePackage_ShouldReturnUnauthorized_WhenInvalidToken()
    {
        // Arrange
        var userService = new UserService(_connection);
        var cardService = new Card(_connection);
        var packageService = new Package(cardService, userService);

        var body = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "Fire Dragon" }, { "Damage", 50.0 } },
            new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "Water Elf" }, { "Damage", 30.0 } },
            new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "Earth Golem" }, { "Damage", 40.0 } },
            new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "Wind Sprite" }, { "Damage", 25.0 } },
            new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "Fire Phoenix" }, { "Damage", 60.0 } }
        };

        // Act
        var (package, statusCode) = packageService.createPackage("Bearer invalid-token", body, _connection);

        // Assert
        Assert.AreEqual(401, statusCode);
        Assert.IsNull(package);
    }

    
    

    [Test]
    public void GetCardType_ShouldReturnCorrectCardType_WhenCardNameIsProvided()
    {
        // Arrange
        var cardName = "Fire Dragon";

        // Act
        var cardType = Card.GetCardType(cardName);

        // Assert
        Assert.AreEqual(CardType.Monster, cardType);  // Fire Dragon should be classified as a Monster
    }

    [Test]
    public void GetElementalType_ShouldReturnCorrectElementType_WhenCardNameIsProvided()
    {
        // Arrange
        var cardName = "Water Elf";

        // Act
        var elementType = Card.GetElementalType(cardName);

        // Assert
        Assert.AreEqual(ElementType.Water, elementType);  // Water Elf should have a Water element
    }

}

