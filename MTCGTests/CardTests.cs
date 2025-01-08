using NUnit.Framework;
using Npgsql;
using SWE.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class CardTests
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
        using (var cmd = new NpgsqlCommand("DELETE FROM public.user_deck; DELETE FROM public.cards; DELETE FROM public.packages;", _connection))
        {
            cmd.ExecuteNonQuery();
        }
    }

    [Test]
    public void SetCardTypeAndElement_ShouldSetCardTypeAndElementCorrectly()
    {
        var card = new Card("FireDragon");

        card.SetCardTypeAndElement();

        Assert.AreEqual(CardType.Monster, card.Type);
        Assert.AreEqual(ElementType.Fire, card.Element);
    }

    [Test]
    public void SetCardTypeAndElement_ShouldSetDefaultTypeAndElement()
    {
        var card = new Card("MysticShield");

        card.SetCardTypeAndElement();
 
        Assert.AreEqual(CardType.Monster, card.Type);
        Assert.AreEqual(ElementType.Normal, card.Element);
    }

    
    [Test]
    public void GetCardType_ShouldReturnCorrectCardType()
    {
        Assert.AreEqual(CardType.Monster, Card.GetCardType("FireDragon"));
        Assert.AreEqual(CardType.Spell, Card.GetCardType("HealingSpell"));
    }

    [Test]
    public void GetElementalType_ShouldReturnCorrectElementalType()
    {
        Assert.AreEqual(ElementType.Fire, Card.GetElementalType("FireDragon"));
        Assert.AreEqual(ElementType.Water, Card.GetElementalType("WaterElf"));
        Assert.AreEqual(ElementType.Normal, Card.GetElementalType("MysticShield"));
    }

    [Test]
    public void Card_ShouldHaveCorrectName_WhenCreated()
    {
        var cardName = "WaterGoblin";

        var card = new Card(cardName);

        Assert.AreEqual(cardName, card.name);
    }


    [Test]
    public void GetCardType_ShouldReturnMonster_ForMonsterCardName()
    {
        string cardName = "FireElf";

        var cardType = Card.GetCardType(cardName);

        Assert.AreEqual(CardType.Monster, cardType);
    }

    [Test]
    public void GetElementalType_ShouldReturnFire_ForFireCardName()
    {
        string cardName = "FireDragon";

        var elementType = Card.GetElementalType(cardName);

        Assert.AreEqual(ElementType.Fire, elementType);
    }

    [Test]
    public void CreatePackage_ShouldCreatePackage_WhenAuthorized()
    {
        var serviceProvider = new ServiceCollection()
        .AddSingleton<Card>()
        .AddSingleton<UserService>()
        .AddSingleton<TcpServer>()
        .BuildServiceProvider();

        var mockConnectionString = "Host=localhost;Username=postgres;Password=fhtw;Database=mtcg_test";
        Card _cardService = new Card(mockConnectionString);
        UserService _userService = new UserService(new NpgsqlConnection(mockConnectionString));
        var auth = "Bearer admin-mtcgToken";
        var receive = new List<Dictionary<string, object>>

    {
        new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "WaterSpell" }, { "Damage", 30 } },
        new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "FireBlast" }, { "Damage", 40 } },
        new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "Earthquake" }, { "Damage", 50 } },
        new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "ThunderStrike" }, { "Damage", 60 } },
        new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "IceBlast" }, { "Damage", 70 } }
    };

        var packageService = new Package(_cardService, _userService);
        var result = packageService.createPackage(auth, receive, new NpgsqlConnection(mockConnectionString));

        Assert.AreEqual(0, result.Item2);
        Assert.IsNotNull(result.Item1);
    }

    [Test]
    public void CreatePackage_ShouldReturnUnauthorized_WhenInvalidToken()
    {
        var serviceProvider = new ServiceCollection()
        .AddSingleton<Card>()
        .AddSingleton<UserService>()
        .AddSingleton<TcpServer>()
        .BuildServiceProvider();

        var mockConnectionString = "Host=localhost;Username=postgres;Password=fhtw;Database=mtcg_test";
        Card _cardService = new Card(mockConnectionString);
        UserService _userService = new UserService(new NpgsqlConnection(mockConnectionString));
        var auth = "Bearer hoax-mtcgToken";
        var receive = new List<Dictionary<string, object>>

    {
        new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "WaterSpell" }, { "Damage", 30 } },
        new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "FireBlast" }, { "Damage", 40 } },
        new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "Earthquake" }, { "Damage", 50 } },
        new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "ThunderStrike" }, { "Damage", 60 } },
        new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "IceBlast" }, { "Damage", 70 } }
    };

        var packageService = new Package(_cardService, _userService);
        var result = packageService.createPackage(auth, receive, new NpgsqlConnection(mockConnectionString));

        Assert.AreEqual(401, result.Item2);
    }

    [Test]
    public void CreatePackage_ShouldReturnBadRequest_WhenNotEnoughCards()
    {
        var serviceProvider = new ServiceCollection()
        .AddSingleton<Card>()
        .AddSingleton<UserService>()
        .AddSingleton<TcpServer>()
        .BuildServiceProvider();

        var mockConnectionString = "Host=localhost;Username=postgres;Password=fhtw;Database=mtcg_test";
        Card _cardService = new Card(mockConnectionString);
        UserService _userService = new UserService(new NpgsqlConnection(mockConnectionString));
        var auth = "Bearer admin-mtcgToken";
        var receive = new List<Dictionary<string, object>>

    {
        new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "WaterSpell" }, { "Damage", 30 } },
        new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "FireBlast" }, { "Damage", 40 } },
        new Dictionary<string, object> { { "Id", Guid.NewGuid() }, { "Name", "Earthquake" }, { "Damage", 50 } },
    };

        var packageService = new Package(_cardService, _userService);
        var result = packageService.createPackage(auth, receive, new NpgsqlConnection(mockConnectionString));

        Assert.AreEqual(400, result.Item2);
    }


}