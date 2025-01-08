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
        var userService = new UserService(_connection);
        var cardService = new Card(_connection);
        var packageService = new Package(cardService, userService);

        var body = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "FireDragon" }, { "Damage", 50.0 } },
            new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "WaterElf" }, { "Damage", 30.0 } },
            new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "EarthGolem" }, { "Damage", 40.0 } },
            new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "WindSprite" }, { "Damage", 25.0 } },
            new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "FirePhoenix" }, { "Damage", 60.0 } }
        };

        var (package, statusCode) = packageService.createPackage("Bearer invalid-token", body, _connection);

        Assert.AreEqual(401, statusCode);
        Assert.IsNull(package);
    }

    
    

    [Test]
    public void GetCardType_ShouldReturnCorrectCardType_WhenCardNameIsProvided()
    {
        var cardName = "FireDragon";

        var cardType = Card.GetCardType(cardName);

        Assert.AreEqual(CardType.Monster, cardType);
    }

    [Test]
    public void GetElementalType_ShouldReturnCorrectElementType_WhenCardNameIsProvided()
    {
        var cardName = "WaterElf";

        var elementType = Card.GetElementalType(cardName);

        Assert.AreEqual(ElementType.Water, elementType);
    }

}

