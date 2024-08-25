using AEBackend;

namespace AEBackend.Repositories.RepositoryUsingEF;
public class UserRepositoryUsingEF : IUserRepository
{
  private readonly UserDBContext _userDBContext;

  public UserRepositoryUsingEF(UserDBContext userDBContext)
  {
    _userDBContext = userDBContext;
  }
  public async Task CreateUser(User user)
  {
    await _userDBContext.Users.AddAsync(user);
    await _userDBContext.SaveChangesAsync();
  }
}