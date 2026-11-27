namespace FrioTrack.Api.Iam.Application.QueryServices;
public interface IAccountAccessQueryService { Task<object> List(string authorization); }
