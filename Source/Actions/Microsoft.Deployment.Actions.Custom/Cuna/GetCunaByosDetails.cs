﻿using Microsoft.Deployment.Common;
using Microsoft.Deployment.Common.ActionModel;
using Microsoft.Deployment.Common.Actions;
using Microsoft.Deployment.Common.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Deployment.Actions.AzureCustom.Cuna
{
    [Export(typeof(IAction))]
    public class GetCunaByosDetails : BaseAction
    {
        public override async Task<ActionResponse> ExecuteActionAsync(ActionRequest request)
        {
            var cunaStatausKey = "CunaStatus";
            var cunaMessageKey = "CunaMessage";

            var userId = request.DataStore.GetValue("userId");
            var contractId = request.DataStore.GetValue("contractId");
            var cunaApiAccessToken = request.DataStore.GetValue("CunaApiAccessToken");
            var pbiToken = request.DataStore.GetJson("PBIToken", "access_token").ToString();

            JwtSecurityToken jwtToken = new JwtSecurityToken(pbiToken);
            var customerUpn = jwtToken.Claims.First(c => c.Type == "upn")?.Value;
            var tenantId = jwtToken.Claims.First(c => c.Type == "tid")?.Value;

            var response = await GetByosDetails(Constants.CunaApiUrl, userId, contractId, customerUpn, tenantId, cunaApiAccessToken);

            if(response["status"] != null)
            {
                request.DataStore.AddToDataStore(cunaStatausKey, response["status"], DataStoreType.Private);

                if (response["status"].ToString().Equals("success", StringComparison.InvariantCultureIgnoreCase) && response["details"] != null)
                {
                    request.DataStore.AddToDataStore("DatapoolName", response["details"]["DatapoolName"], DataStoreType.Private);
                    request.DataStore.AddToDataStore("DatapoolDescription", response["details"]["DatapoolDescription"], DataStoreType.Private);
                    request.DataStore.AddToDataStore("KeyVaultSubscriptionId", response["details"]["KeyVaultSubscriptionId"], DataStoreType.Private);
                    request.DataStore.AddToDataStore("KeyVaultResourceGroupName", response["details"]["KeyVaultResourceGroupName"], DataStoreType.Private);
                    request.DataStore.AddToDataStore("KeyVaultName", response["details"]["KeyVaultName"], DataStoreType.Private);
                    request.DataStore.AddToDataStore("KeyVaultSecretPath", response["details"]["KeyVaultSecretPath"], DataStoreType.Private);
                }
                else
                {
                    if(response["message"] != null)
                    {
                        request.DataStore.AddToDataStore(cunaMessageKey, response["message"], DataStoreType.Private);
                    }
                }
            }
            else
            {
                request.DataStore.AddToDataStore(cunaStatausKey, "failure", DataStoreType.Private);
                request.DataStore.AddToDataStore(cunaMessageKey, "An unknown error occured", DataStoreType.Private);
            }
            
            return new ActionResponse(ActionStatus.Success, response.ToString(), true);
        }

        public async Task<JObject> GetByosDetails(
            string cunaApiUrl,
            string userId,
            string contractId,
            string customerUpn,
            string tenantId,
            string authToken
        )
        {
            var postData = new Dictionary<string, string>
            {
                {"userId", userId},
                {"contractId", contractId},
                {"customerUpn", customerUpn},
                {"customerTenant", tenantId }
            };

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                var content = new StringContent(JsonConvert.SerializeObject(postData), Encoding.UTF8, "application/json");

                var response = await client.PostAsync(cunaApiUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("Unable to contact Cuna API service.");
                }
                var jsonBody = await response.Content.ReadAsStringAsync();
                return GetJsonObject(jsonBody);
            }
        }

        public JObject GetJsonObject(string jsonBody)
        {
            try
            {
                var obj = JsonUtility.GetJsonObjectFromJsonString(jsonBody);
                return obj;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}