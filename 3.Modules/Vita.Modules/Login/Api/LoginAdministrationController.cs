﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Web;

namespace Vita.Modules.Login.Api {
  [ApiRoutePrefix("logins"), LoggedInOnly, Secured, ApiGroup("Login/Administration")]
  public class LoginAdministrationController : SlimApiController {
    ILoginAdministrationService _adminService; 

    public override void InitController(OperationContext context) {
      base.InitController(context);
      _adminService = Context.App.GetService<ILoginAdministrationService>(); 
    }

    [ApiGet, ApiRoute("")]
    public SearchResults<LoginInfo> SearchLogins([FromUrl] LoginSearch search) {
      var logins = _adminService.SearchLogins(Context, search);
      var result = logins.Convert<ILogin, LoginInfo>(lg => lg.ToModel());
      return result; 
    }

    [ApiGet, ApiRoute("{id}")]
    public LoginInfo GetLogin(Guid id) {
      var session = Context.OpenSecureSession();
      var login = session.GetEntity<ILogin>(id);
      if(login == null)
        return null;
      return login.ToModel(); 
    }


    [ApiPut, ApiRoute("{loginid}/temppassword")]
    public OneTimePasswordInfo SetOneTimePassword (Guid loginId) {
      var session = Context.OpenSession();
      var login = _adminService.GetLogin(session, loginId);
      Context.ThrowIfNull(login, ClientFaultCodes.ObjectNotFound, "Login", "Login not found.");
      var loginSettings = Context.App.GetConfig<LoginModuleSettings>();
      string password = _adminService.GenerateTempPassword();
      _adminService.SetOneTimePassword(login, password);
      return new OneTimePasswordInfo() { Password = password, ExpiresHours = (int) loginSettings.OneTimePasswordExpiration.TotalHours }; 
    }

    public class LoginStatusUpdate {
      public bool? Disable { get; set; }
      public bool? Suspend { get; set; }
    }

    [ApiPut, ApiRoute("{loginid}/status")]
    public void UpdateStatus(Guid loginId, [FromUrl] LoginStatusUpdate update) {
      var session = Context.OpenSession();
      var login = _adminService.GetLogin(session, loginId);
      Context.ThrowIfNull(login, ClientFaultCodes.ObjectNotFound, "Login", "Login not found.");
      _adminService.UpdateStatus(login, update.Disable, update.Suspend);
    }

  }
}//ns
