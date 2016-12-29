﻿using IdentityServer3.Core;
using IdentityServer3.Core.Configuration;
using IdentityServer3.Core.Configuration.Hosting;
using IdentityServer3.Core.Logging;
using IdentityServer3.Core.Services;
using IdentityServer3.Host.Config;
using Microsoft.Owin;
using Microsoft.Owin.Security.Facebook;
using Microsoft.Owin.Security.Google;
using Microsoft.Owin.Security.OpenIdConnect;
using Microsoft.Owin.Security.Twitter;
using Microsoft.Owin.Security.WsFederation;
using Owin;
using System.Collections.Generic;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Infrastructure;
using Autofac;
using System;
using System.IdentityModel.Tokens;
using Host.Configuration;

namespace Host.Web.Custom
{
    public static class IdentityServerExtension
    {
        public static IAppBuilder UseCustomIdentityServer(this IAppBuilder app)
        {
            // uncomment to enable HSTS headers for the host
            // see: https://developer.mozilla.org/en-US/docs/Web/Security/HTTP_strict_transport_security
            //app.UseHsts();

            app.Map("/core", coreApp =>
            {
                var factory = new IdentityServerServiceFactory()
                    .UseInMemoryUsers(Users.Get())
                    .UseInMemoryClients(Clients.Get())
                    .UseInMemoryScopes(Scopes.Get());

                factory.AddCustomGrantValidators();
                factory.AddCustomTokenResponseGenerator();

                factory.ConfigureClientStoreCache();
                factory.ConfigureScopeStoreCache();
                factory.ConfigureUserServiceCache();

                var idsrvOptions = new IdentityServerOptions
                {
                    Factory = factory,
                    SigningCertificate = Cert.Load(),

                    Endpoints = new EndpointOptions
                    {
                        // replaced by the introspection endpoint in v2.2
                        EnableAccessTokenValidationEndpoint = false
                    },

                    AuthenticationOptions = new AuthenticationOptions
                    {
                        IdentityProviders = ConfigureIdentityProviders
                        //EnablePostSignOutAutoRedirect = true
                    },

                    //LoggingOptions = new LoggingOptions
                    //{
                    //    EnableKatanaLogging = true
                    //},

                    //EventsOptions = new EventsOptions
                    //{
                    //    RaiseFailureEvents = true,
                    //    RaiseInformationEvents = true,
                    //    RaiseSuccessEvents = true,
                    //    RaiseErrorEvents = true
                    //}
                };

                //START CUSTOM IdentityServer
                coreApp.Use<RequireSslMiddleware>();
                idsrvOptions.Validate();

                // turn off weird claim mappings for JWTs
                JwtSecurityTokenHandler.InboundClaimTypeMap = new Dictionary<string, string>();
                JwtSecurityTokenHandler.OutboundClaimTypeMap = new Dictionary<string, string>();

                if (idsrvOptions.LoggingOptions.EnableKatanaLogging)
                {
                    coreApp.SetLoggerFactory(new LibLogKatanaLoggerFactory());
                }

                coreApp.UseEmbeddedFileServer();

                coreApp.ConfigureRequestId();
                coreApp.ConfigureDataProtectionProvider(idsrvOptions);
                coreApp.ConfigureIdentityServerBaseUrl(idsrvOptions.PublicOrigin);
                coreApp.ConfigureIdentityServerIssuer(idsrvOptions);

                // this needs to be earlier than the autofac middleware so anything is disposed and re-initialized
                // if we send the request back into the pipeline to render the logged out page
                coreApp.ConfigureRenderLoggedOutPage();

                var container = AutofacConfig.Configure(idsrvOptions);
                coreApp.UseAutofacMiddleware(container);

                coreApp.UseCors(container.Resolve<ICorsPolicyService>());
                coreApp.ConfigureCookieAuthentication(idsrvOptions.AuthenticationOptions.CookieOptions, idsrvOptions.DataProtector);

                // this needs to be before external middleware
                coreApp.ConfigureSignOutMessageCookie();

                if (idsrvOptions.PluginConfiguration != null)
                {
                    idsrvOptions.PluginConfiguration(coreApp, idsrvOptions);
                }

                if (idsrvOptions.AuthenticationOptions.IdentityProviders != null)
                {
                    idsrvOptions.AuthenticationOptions.IdentityProviders(coreApp, Constants.ExternalAuthenticationType);
                }


                coreApp.ConfigureHttpLogging(idsrvOptions.LoggingOptions);

                SignatureConversions.AddConversions(coreApp);

                var httpConfig = WebApiConfig.Configure(idsrvOptions, container);
                coreApp.UseAutofacWebApi(httpConfig);
                coreApp.UseWebApi(httpConfig);

                //using (var child = container.CreateScopeWithEmptyOwinContext())
                //{
                //    var eventSvc = child.Resolve<IEventService>();
                //    // TODO -- perhaps use AsyncHelper instead?
                //    DoStartupDiagnosticsAsync(options, eventSvc).Wait();
                //}
            });

            return app;
        }

        public static void ConfigureIdentityProviders(IAppBuilder app, string signInAsType)
        {
            var google = new GoogleOAuth2AuthenticationOptions
            {
                AuthenticationType = "Google",
                Caption = "Google",
                SignInAsAuthenticationType = signInAsType,

                ClientId = "767400843187-8boio83mb57ruogr9af9ut09fkg56b27.apps.googleusercontent.com",
                ClientSecret = "5fWcBT0udKY7_b6E3gEiJlze"
            };
            app.UseGoogleAuthentication(google);

            var fb = new FacebookAuthenticationOptions
            {
                AuthenticationType = "Facebook",
                Caption = "Facebook",
                SignInAsAuthenticationType = signInAsType,

                AppId = "676607329068058",
                AppSecret = "9d6ab75f921942e61fb43a9b1fc25c63"
            };
            app.UseFacebookAuthentication(fb);

            var twitter = new TwitterAuthenticationOptions
            {
                AuthenticationType = "Twitter",
                Caption = "Twitter",
                SignInAsAuthenticationType = signInAsType,

                ConsumerKey = "N8r8w7PIepwtZZwtH066kMlmq",
                ConsumerSecret = "df15L2x6kNI50E4PYcHS0ImBQlcGIt6huET8gQN41VFpUCwNjM"
            };
            app.UseTwitterAuthentication(twitter);

            var aad = new OpenIdConnectAuthenticationOptions
            {
                AuthenticationType = "aad",
                Caption = "Azure AD",
                SignInAsAuthenticationType = signInAsType,

                Authority = "https://login.windows.net/4ca9cb4c-5e5f-4be9-b700-c532992a3705",
                ClientId = "65bbbda8-8b85-4c9d-81e9-1502330aacba",
                RedirectUri = "https://localhost:44333/core/aadcb"
            };

            app.UseOpenIdConnectAuthentication(aad);

            var adfs = new WsFederationAuthenticationOptions
            {
                AuthenticationType = "adfs",
                Caption = "ADFS",
                SignInAsAuthenticationType = signInAsType,
                CallbackPath = new PathString("/core/adfs"),

                MetadataAddress = "https://adfs.leastprivilege.vm/federationmetadata/2007-06/federationmetadata.xml",
                Wtrealm = "urn:idsrv3"
            };
            app.UseWsFederationAuthentication(adfs);

            var was = new WsFederationAuthenticationOptions
            {
                AuthenticationType = "was",
                Caption = "Windows",
                SignInAsAuthenticationType = signInAsType,
                CallbackPath = new PathString("/core/was"),

                MetadataAddress = "https://localhost:44350",
                Wtrealm = "urn:idsrv3"
            };
            app.UseWsFederationAuthentication(was);
        }
    }
}