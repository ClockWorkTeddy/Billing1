using Grpc.Core;

namespace Billing.Services;

public class BillingService : Billing.BillingBase
{
    static List<User> Users = new();
    static List<CoinObj> Coins = new();
    private readonly ILogger<BillingService> _logger;
    public BillingService(ILogger<BillingService> logger)
    {
        _logger = logger;
    }

    public override async Task ListUsers(None none, 
                                         IServerStreamWriter<UserProfile> responseStream, 
                                         ServerCallContext context)
    {
        if (Users.Count == 0)
            CreateUsers();

        foreach (var user in Users)
        {
            user.Profile.Amount = user.Amount;
            await responseStream.WriteAsync(user.Profile);
        }
    }

    public override Task<Response> CoinsEmission(EmissionAmount emissionAmmount, 
                                                 ServerCallContext context)
    {
        PredictAmmount(emissionAmmount.Amount);

        if (ValidateEmission(emissionAmmount.Amount))
        {
            StartEmission(emissionAmmount.Amount);
            
            return Task.FromResult(new Response
            {
                Status = Response.Types.Status.Ok,
                Comment = "Emission successful"                
            });
        }
        else
        {
            return Task.FromResult(new Response
            {
                Status = Response.Types.Status.Failed,
                Comment = "Not enought coins!"
            });
        }
    }
    public override Task<Response> MoveCoins(MoveCoinsTransaction transaction, 
                                             ServerCallContext context)
    {
        User? srcUser = GetUserByName(transaction.SrcUser);
        User? dstUser = GetUserByName(transaction.DstUser);

        if (srcUser != null && dstUser != null)
            if (srcUser.Amount >= transaction.Amount)
            {
                MakeTransaction(srcUser, dstUser, transaction.Amount);
                
                return Task.FromResult(new Response
                {
                    Status = Response.Types.Status.Ok,
                    Comment = "Transaction successful"
                });
            }
            else
            {
                return Task.FromResult(new Response
                {
                    Status = Response.Types.Status.Failed,
                    Comment = "User doesn't have enough coins!"
                });
            }
        else
            return Task.FromResult(new Response
            {
                Status = Response.Types.Status.Failed,
                Comment = "User not found!"
            });
    }

    public override Task<Coin> LongestHistoryCoin(None none, 
                                                  ServerCallContext context)
    {
        Coins.Sort();
        CoinObj superCoin = Coins[0];

        return Task.FromResult(new Coin
        {
            Id = superCoin.Id,
            History = GetHistory(superCoin)
        });
    }

  private void CreateUsers()
    {
        Users.Add(new User("boris", 5000));
        Users.Add(new User("maria", 1000));
        Users.Add(new User("oleg", 800));
        
        Users.Sort();

        foreach (var user in Users)
            user.Profile = new UserProfile() { Name = user.Name, Amount = 0};
    }

    private long GetOverallRating()
    {
        long overallRating = 0;

        foreach(var user in Users)
            overallRating += user.Rating;

        return overallRating;
    }

    private void PredictAmmount(long amount)
    {
        long overallRating = GetOverallRating();
        long coinPrice = overallRating / amount;
        long freeCoins = amount;

        foreach (var user in Users)
        {
            user.PlannedAmmount = user.Rating / coinPrice;
            
            if (user.PlannedAmmount == 0)
                user.PlannedAmmount = 1; 

            freeCoins -= user.PlannedAmmount;
        }

        if (freeCoins > 0)
            Users.Last().PlannedAmmount += freeCoins;
    }

    private bool ValidateEmission(long amount)
    {
        long overallPlannedAmount = 0;

        foreach(var user in Users)
            overallPlannedAmount += user.PlannedAmmount;

        return amount == overallPlannedAmount; 
    }

    private void StartEmission(long amount)
    {
        long coinId = 0;

        foreach (var user in Users)
        {
            for (int i = 0; i < user.PlannedAmmount; i++)
            {
                user.Coins.Add(new CoinObj(user.Name, coinId));
                coinId++;
            }

            Coins.AddRange(user.Coins);
        }
    }

    private User? GetUserByName(string userName)
    {
        return Users.FirstOrDefault(u => u.Name == userName);
    }

    private void MakeTransaction(User srcUser, User dstUser, long amount)
    {
        for (int i = 0; i < amount; i++)
        {
            srcUser.Coins[i].Owners.Add(dstUser.Name);
            dstUser.Coins.Add(srcUser.Coins[i]);
        }

        for (int i = 0; i < amount; i++)
            srcUser.Coins.RemoveAt(0);
    }

    private string GetHistory(CoinObj superCoin)
    {
        System.Text.StringBuilder history = new System.Text.StringBuilder();

        foreach (var owner in superCoin.Owners)
            history.Append($"{owner};");
        
        return history.ToString();
    }

}

public class User : IComparable<User>
{
    public string Name {get; set;}
    public long Amount 
    {
        get
        {
            return Coins.Count;
        } 
    }
    public long PlannedAmmount {get; set;} = 0;
    public long Rating {get; set;}
    public UserProfile? Profile {get; set;} = new();
    public List<CoinObj> Coins {get; set;} = new();
    public User(string name, long rating)
    {
        Name = name;
        Rating = rating;
    }

    public int CompareTo(User? user)
    {
        if (user is null)
            throw new NullReferenceException();
        else if (Rating > user.Rating)
            return 1;
        else if (Rating < user.Rating)
            return -1;
        else 
            return 0;
    }
}

public class CoinObj : IComparable<CoinObj>
{
    public long Id;
    public List<string> Owners {get; set;} = new();

    public CoinObj(string owner, long id)
    {
        Id = id;
        Owners.Add(owner);
    }

    public int CompareTo(CoinObj? coin)
    {
        if (coin is null)
            throw new NullReferenceException();
        else 
            return coin.Owners.Count - Owners.Count;
    }
}
