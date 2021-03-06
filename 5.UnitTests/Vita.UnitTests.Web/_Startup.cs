﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Web.Http.SelfHost;
using System.Diagnostics;

using Vita.Entities;
using Vita.Entities.Services;
using Vita.Web;
using Vita.Data;
using Vita.Data.Model;
using Vita.Modules.Logging;
using Vita.Data.MsSql;

using Vita.Common;
using Vita.Modules.WebClient;
using Vita.Modules.DbInfo;
using Vita.UnitTests.Common;
using Vita.Samples.BookStore;
using Vita.Samples.BookStore.SampleData;
using Vita.Modules.Notifications;
using Vita.Entities.Web;

namespace Vita.UnitTests.Web {

  public static class Startup {
    public static BooksEntityApp BooksApp; 
    public static HttpSelfHostServer Server;
    public static WebApiClient Client;
    public static NotificationListener NotificationListener; 
    public static string LogFilePath;

    public static void Init() {
      try {
        InitImpl(); 
      } catch(Exception ex) {
        Debug.WriteLine(ex.ToLogString());
        throw;
      }
    }

    public static void InitImpl() {
      if(BooksApp != null)
        return;
      LogFilePath = ConfigurationManager.AppSettings["LogFilePath"];
      DeleteLocalLogFile();

      var protectedSection = (NameValueCollection)ConfigurationManager.GetSection("protected");
      var loginCryptoKey = protectedSection["LoginInfoCryptoKey"];
      var connString = protectedSection["MsSqlConnectionString"];
      var logConnString = protectedSection["MsSqlLogConnectionString"];

      BooksApp = new BooksEntityApp(loginCryptoKey);
      //Add mock email/sms service
      NotificationListener = new NotificationListener(BooksApp, blockAll: true);
      //Set magic captcha in login settings, so we can pass captcha in unit tests
      var loginStt = BooksApp.GetConfig<Vita.Modules.Login.LoginModuleSettings>();
      loginStt.MagicCaptcha = "Magic"; 
      BooksApp.Init(); 
      //connect to database
      var driver = MsSqlDbDriver.Create(connString);
      var dbOptions = MsSqlDbDriver.DefaultMsSqlDbOptions;
      var dbSettings = new DbSettings(driver, dbOptions, connString, upgradeMode: DbUpgradeMode.Always); // schemas);
      var resetDb = ConfigurationManager.AppSettings["ResetDatabase"] == "true";
      if(resetDb)
        Vita.UnitTests.Common.TestUtil.DropSchemaObjects(dbSettings);
      BooksApp.ConnectTo(dbSettings);
      var logDbSettings = new DbSettings(driver, dbOptions, logConnString);
      BooksApp.LoggingApp.ConnectTo(logDbSettings);
      BooksApp.LoggingApp.LogPath = LogFilePath;
      TestUtil.DeleteAllData(BooksApp, exceptEntities: new [] {typeof(IDbInfo), typeof(IDbModuleInfo)});
      TestUtil.DeleteAllData(BooksApp.LoggingApp);

      SampleDataGenerator.CreateUnitTestData(BooksApp);

      // Start service 
      var serviceUrl = ConfigurationManager.AppSettings["ServiceUrl"];
      var jsonNames = ConfigurationManager.AppSettings["JsonStyleNames"] == "true";
      var jsonMappingMode = jsonNames ? ApiNameMapping.UnderscoreAllLower : ApiNameMapping.Default;
      StartService(serviceUrl, jsonMappingMode);
      // create client
      var clientContext = new OperationContext(BooksApp);
      // change options to None to disable logging of test client calls        
      Client = new WebApiClient(clientContext, serviceUrl, clientName : "TestClient", nameMapping: jsonMappingMode, 
          options: ClientOptions.EnableLog, badRequestContentType: typeof(List<ClientFault>));
      WebApiClient.SharedHttpClientHandler.AllowAutoRedirect = false; //we need it for Redirect test
    }

    public static void StartService(string baseAddress, ApiNameMapping jsonMappingMode) {
      var config = new HttpSelfHostConfiguration(baseAddress);
      // Add top-level handler first
      config.MessageHandlers.Add(new DiagnosticsHttpHandler());
      //Enable set-time-offset function in diagnostics controller - this should be done in testing environment only
      DiagnosticsController.EnableTimeOffset = true; 
      //Add extra controllers we use in tests
      BooksApp.ApiConfiguration.RegisterControllerType(typeof(SpecialMethodsController));
      BooksApp.ApiConfiguration.RegisterController(new SingletonController("Some config info"));
      // Explicitly load ApiController type - to ensure WebApi can find it. 
      // Needed for classic API controllers that live in a separate assembly (not in data service host project). Not needed for SlimApi controllers
      var contrTypes = new Type[] { typeof(ClassicWebApiController) };
      WebHelper.ConfigureWebApi(config, BooksApp, LogLevel.Details,
         WebHandlerOptions.ReturnBadRequestOnAuthenticationRequired | WebHandlerOptions.ReturnExceptionDetails, jsonMappingMode);
      config.MaxReceivedMessageSize = int.MaxValue;
      config.MaxBufferSize = int.MaxValue;
      Server = new HttpSelfHostServer(config); 
      Task.Run(() => Server.OpenAsync());
      Debug.WriteLine("The service is running on URL: " + baseAddress);
    }

    public static void FlushLogs() {
      BooksApp.Flush();
    }

    //Init log and delete log file only once at app startup; important when running in batch mode for multiple servers
    static bool _logFileDeleted;
    internal static void DeleteLocalLogFile() {
      if(_logFileDeleted)
        return;
      if(File.Exists(LogFilePath))
        File.Delete(LogFilePath);
      _logFileDeleted = true;
    }

    public static NotificationMessage GetLastMessageTo(string email) {
      System.Threading.Thread.Sleep(50); //sending messages is async, make sure bkgr thread done its job
      return NotificationListener.GetLastMessageTo(email);
    }

  }
}
