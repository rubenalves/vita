﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Web.Implementation;

namespace Vita.Entities.Web {

  public class SlimApiController : ISlimApiControllerInit {
    protected OperationContext Context;
    static bool _webInitialized;

    public virtual void InitController(OperationContext context) {
      if (!_webInitialized) {
        //Perform one-time notification for first web call 
        context.App.WebInitilialize(context.WebContext);
        _webInitialized = true; 
      }
      Context = context;
      Context.DbConnectionMode = DbConnectionReuseMode.KeepOpen;
    }

    /// <summary>Sets up the method to return plain text in the response body. </summary>
    /// <param name="value">The string to return.</param>
    /// <returns>The value of parameter.</returns>
    /// <remarks>This method allows you to return the string as-is in the response body content, without 
    /// interference of Json serializer. If you try to return a string from a controller method without
    /// doing anything else causes the Json deserializer to format it as a Json value, 
    /// enclosed in double quotes. If you want to avoid this and return the string as-is,
    /// use this method. </remarks>
    protected string ReturnPlainText(string value) {
      Context.WebContext.OutgoingResponseContent = value;
      return value;
    }


  }
}//ns
