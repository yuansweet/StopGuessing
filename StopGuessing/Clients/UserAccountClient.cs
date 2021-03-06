﻿using StopGuessing.DataStructures;
using StopGuessing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StopGuessing.Controllers;

namespace StopGuessing.Clients
{
    public class UserAccountClient
    {
        int NumberOfRedundentHostsToCacheEachAccount => Math.Min(3, _responsibleHosts.Count); // FUTURE -- use configuration file value
        private TimeSpan DefaultTimeout { get; } = new TimeSpan(0, 0, 0, 0, 500); // FUTURE use configuration value


        private UserAccountController _localUserAccountController;
        private readonly IDistributedResponsibilitySet<RemoteHost> _responsibleHosts;
        private readonly RemoteHost _localHost;

        public UserAccountClient(IDistributedResponsibilitySet<RemoteHost> responsibleHosts, RemoteHost localHost)
        {
            _responsibleHosts = responsibleHosts;
            _localHost = localHost;
        }

        public void SetLocalUserAccountController(UserAccountController userAccountController)
        {
            _localUserAccountController = userAccountController;
        }

        public List<RemoteHost> GetServersResponsibleForCachingAnAccount(string usernameOrAccountId)
        {
            return _responsibleHosts.FindMembersResponsible(usernameOrAccountId,
                NumberOfRedundentHostsToCacheEachAccount);
        }

        public List<RemoteHost> GetServersResponsibleForCachingAnAccount(UserAccount account)
        {
            return GetServersResponsibleForCachingAnAccount(account.UsernameOrAccountId);
        }


        /// <summary>
        /// Calls UserAccountController.TryGetCreditAsync()
        /// </summary>
        /// <param name="usernameOrAccountId"></param>
        /// <param name="amountToGet"></param>
        /// <param name="serversResponsibleForCachingThisAccount"></param>
        /// <param name="timeout"></param>
        /// <param name="cancellationToken">To allow the async call to be cancelled, such as in the event of a timeout.</param>
        /// <returns></returns>
        public async Task<bool> TryGetCreditAsync(string usernameOrAccountId,
            float amountToGet = 1f,
            List<RemoteHost> serversResponsibleForCachingThisAccount = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (serversResponsibleForCachingThisAccount == null)
            {
                serversResponsibleForCachingThisAccount = GetServersResponsibleForCachingAnAccount(usernameOrAccountId);
            }
            return await RestClientHelper.TryServersUntilOneResponds(
                iterationParameters: serversResponsibleForCachingThisAccount,
                timeBetweenRetries: timeout ?? DefaultTimeout,
                actionToTry: async (server, localTimeout) => server.Equals(_localHost)
                    ? await _localUserAccountController.LocalTryGetCreditAsync(
                        usernameOrAccountId, amountToGet,
                        serversResponsibleForCachingThisAccount, cancellationToken)
                    : await RestClientHelper.PostAsync<bool>(server.Uri,
                        "/api/UserAccount/" + Uri.EscapeUriString(usernameOrAccountId) + "/TryGetCredit",
                        new Object[]
                        {
                            new KeyValuePair<string, float>("amountToGet", 1f),
                            new KeyValuePair<string, List<RemoteHost>>("serversResponsibleForCachingThisAccount",
                                serversResponsibleForCachingThisAccount)
                        }, localTimeout, cancellationToken),
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Calls UserAccountController.GetAsync(), via a REST GET request or directly (if the UserAccount is managed locally),
        /// to fetch a UserAccount records by its usernameOrAccountId.
        /// </summary>
        /// <param name="accountId">The unique ID of the account to fetch.</param>
        /// <param name="serversResponsibleForCachingThisAccount"></param>
        /// <param name="cancellationToken">To allow the async call to be cancelled, such as in the event of a timeout.</param>
        /// <param name="timeout"></param>
        /// <returns>The account record that was retrieved.</returns>
        public async Task<UserAccount> GetAsync(string accountId,
            TimeSpan? timeout = null,
            List<RemoteHost> serversResponsibleForCachingThisAccount = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (serversResponsibleForCachingThisAccount == null)
            {
                serversResponsibleForCachingThisAccount = GetServersResponsibleForCachingAnAccount(accountId);
            }

            string pathUri = "/api/UserAccount/" + Uri.EscapeUriString(accountId);

            return await RestClientHelper.TryServersUntilOneResponds(
                serversResponsibleForCachingThisAccount,
                timeout ?? DefaultTimeout,
                async (server, innerTimeout) =>
                {
                    if (server.Equals(_localHost))
                        return await _localUserAccountController.LocalGetAsync(accountId, serversResponsibleForCachingThisAccount, cancellationToken);
                    else
                        return await RestClientHelper.GetAsync<UserAccount>(server.Uri, pathUri, timeout:innerTimeout, cancellationToken: cancellationToken);
                }, cancellationToken);
        }

        /// <summary>
        /// Calls UserAccountController.PutAsync, via a REST PUT request or directly (if the UserAccount is managed locally),
        /// to store UserAccount to stable store and to the local cache of whichever machines are in charge of maintaining
        /// a copy in memory.
        /// </summary>
        /// <param name="account">The account to store.</param>
        /// <param name="timeout"></param>
        /// <param name="cancellationToken">To allow the async call to be cancelled, such as in the event of a timeout.</param>
        /// <returns>The account record as stored.</returns>
        public async Task<UserAccount> PutAsync(UserAccount account,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            List<RemoteHost> serversResponsibleFOrCachingAnAccount = GetServersResponsibleForCachingAnAccount(account);

            return await RestClientHelper.TryServersUntilOneResponds(
                serversResponsibleFOrCachingAnAccount,
                timeout ?? DefaultTimeout,
                async (server, innerTimeout) =>
                    server.Equals(_localHost)
                        ? await
                            _localUserAccountController.PutAsync(account, false, serversResponsibleFOrCachingAnAccount,
                                cancellationToken)
                        : await RestClientHelper.PutAsync<UserAccount>(
                            server.Uri,
                            "/api/UserAccount/" + Uri.EscapeUriString(account.UsernameOrAccountId),
                            new Object[]
                            {
                                new KeyValuePair<string, UserAccount>("account", account),
                                new KeyValuePair<string, bool>("cacheOnly", false),
                                new KeyValuePair<string, List<RemoteHost>>("serversResponsibleFOrCachingAnAccount",
                                    serversResponsibleFOrCachingAnAccount),
                            }, innerTimeout, cancellationToken),
                cancellationToken);
        }

        public async Task PutCacheOnlyAsync(UserAccount account,
            List<RemoteHost> serversResponsibleForCachingThisAccount,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            await Task.WhenAll(serversResponsibleForCachingThisAccount.Where(server => !server.Equals(_localHost)).Select(
                async server => await RestClientHelper.PutAsync(
                    server.Uri,
                    "/api/UserAccount/" + Uri.EscapeUriString(account.UsernameOrAccountId), new Object[]
                    {
                        new KeyValuePair<string, UserAccount>("account", account),
                        new KeyValuePair<string, bool>("cacheOnly", true),
                    }, timeout, cancellationToken)
                ));
        }

        public void PutCacheOnlyInBackground(UserAccount account,
            List<RemoteHost> serversResponsibleForCachingThisAccount,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task.Run(() => PutCacheOnlyAsync(account, serversResponsibleForCachingThisAccount, 
                timeout, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// When a user has provided the correct password for an account, use it to decrypt the key that stores
        /// previous failed password attempts, use that key to decrypt that passwords used in those attempts,
        /// and determine whether they passwords were incorrect because they were typos--passwords similar to,
        /// but a small edit distance away from, the correct password.
        /// </summary>
        /// <param name="usernameOrAccountId">The username or account ID of the account for which the client has authenticated using the correct password.</param>
        /// <param name="correctPassword">The correct password provided by the client.</param>
        /// <param name="phase1HashOfCorrectPassword">The phase 1 hash of the correct password
        /// (we could re-derive this, the hash should be expensive to calculate and so we don't want to replciate the work unnecessarily.)</param>
        /// <param name="ipAddressToExcludeFromAnalysis">This is used to prevent the analysis fro examining LoginAttempts from this IP.
        /// We use it because it's more efficient to perform the analysis for that IP as part of the process of evaluting whether
        /// that IP should be blocked or not.</param>
        /// <param name="serversResponsibleForCachingThisAccount"></param>
        /// <param name="timeout"></param>
        /// <param name="cancellationToken">To allow the async call to be cancelled, such as in the event of a timeout.</param>
        /// <returns>The number of LoginAttempts updated as a result of the analyis.</returns>
        public async Task UpdateOutcomesUsingTypoAnalysisAsync(string usernameOrAccountId,
            string correctPassword,
            byte[] phase1HashOfCorrectPassword,
            System.Net.IPAddress ipAddressToExcludeFromAnalysis,
            List<RemoteHost> serversResponsibleForCachingThisAccount,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (serversResponsibleForCachingThisAccount == null)
            {
                serversResponsibleForCachingThisAccount = GetServersResponsibleForCachingAnAccount(usernameOrAccountId);
            }

            await RestClientHelper.TryServersUntilOneResponds(
                iterationParameters: serversResponsibleForCachingThisAccount,
                actionToTry: async (server, innerTimeout) =>
                {
                    if (server.Equals(_localHost))
                    {
                        await _localUserAccountController.UpdateOutcomesUsingTypoAnalysisAsync(usernameOrAccountId,
                            correctPassword,
                            phase1HashOfCorrectPassword, ipAddressToExcludeFromAnalysis,
                            serversResponsibleForCachingThisAccount, cancellationToken);
                    }
                    else
                    {
                        await RestClientHelper.PostAsync(server.Uri,
                            "/api/UserAccount/" + Uri.EscapeUriString(usernameOrAccountId), new Object[]
                            {
                                new KeyValuePair<string, string>("correctPassword", correctPassword),
                                new KeyValuePair<string, byte[]>("phase1HashOfCorrectPassword",
                                    phase1HashOfCorrectPassword),
                                new KeyValuePair<string, System.Net.IPAddress>("ipAddressToExcludeFromAnalysis",
                                    ipAddressToExcludeFromAnalysis)
                            },
                            timeout: innerTimeout,
                            cancellationToken: cancellationToken);
                    }
                },
                timeBetweenRetries: timeout ?? DefaultTimeout,
                cancellationToken: cancellationToken);
        }


        public void UpdateOutcomesUsingTypoAnalysisInBackground(string usernameOrAccountId, string correctPassword,
            byte[] phase1HashOfCorrectPassword,
            System.Net.IPAddress ipAddressToExcludeFromAnalysis,
            List<RemoteHost> serversResponsibleForCachingThisAccount,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task.Run(() => UpdateOutcomesUsingTypoAnalysisAsync(usernameOrAccountId, correctPassword,
                phase1HashOfCorrectPassword, ipAddressToExcludeFromAnalysis,
                serversResponsibleForCachingThisAccount, timeout, cancellationToken),
                cancellationToken);
        }


        /// <summary>
        /// Update to UserAccount record to incoroprate what we've learned from a LoginAttempt.
        /// 
        /// If the login attempt was successful (Outcome==CrednetialsValid) then we will want to
        /// track the cookie used by the client as we're more likely to trust this client in the future.
        /// If the login attempt was a failure, we'll want to add this attempt to the length-limited
        /// sequence of faield login attempts.
        /// </summary>
        /// <param name="attempt">The attempt to incorporate into the account's records</param>
        /// <param name="serversResponsibleForCachingThisAccount"></param>
        /// <param name="timeout"></param>
        /// <param name="cancellationToken">To allow the async call to be cancelled, such as in the event of a timeout.</param>
        public async Task UpdateForNewLoginAttemptAsync(LoginAttempt attempt,
            List<RemoteHost> serversResponsibleForCachingThisAccount = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (serversResponsibleForCachingThisAccount == null)
            {
                serversResponsibleForCachingThisAccount =
                    GetServersResponsibleForCachingAnAccount(attempt.UsernameOrAccountId);
            }

            await RestClientHelper.TryServersUntilOneResponds(
                serversResponsibleForCachingThisAccount,
                timeout ?? DefaultTimeout,
                async (server, innerTimeout) =>
                {
                    if (server.Equals(_localHost))
                        await _localUserAccountController.UpdateForNewLoginAttemptAsync(
                            attempt.UsernameOrAccountId, attempt, false,
                            serversResponsibleForCachingThisAccount,
                            cancellationToken);
                    else
                        await RestClientHelper.PostAsync(server.Uri,
                            "/api/UserAccount/" +
                            Uri.EscapeUriString(attempt.UsernameOrAccountId), new Object[]
                            {
                                new KeyValuePair<string, LoginAttempt>("attempt", attempt),
                                new KeyValuePair<string, bool>(
                                    "onlyUpdateTheInMemoryCacheOfTheAccount", false),
                                new KeyValuePair<string, List<RemoteHost>>(
                                    "serversResponsibleForCachingThisAccount",
                                    serversResponsibleForCachingThisAccount)
                            }, innerTimeout, cancellationToken);
                },
                cancellationToken);
        }

        public void UpdateForNewLoginAttemptInBackground(LoginAttempt attempt,
            bool onlyUpdateTheInMemoryCacheOfTheAccount = false,
            List<RemoteHost> serversResponsibleForCachingThisAccount = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task.Run(() => UpdateForNewLoginAttemptAsync(attempt,
                serversResponsibleForCachingThisAccount,
                timeout, cancellationToken), cancellationToken);
        }


        public async Task UpdateForNewLoginAttemptCacheOnlyAsync(LoginAttempt attempt,
            List<RemoteHost> serversResponsibleForCachingThisAccount = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (serversResponsibleForCachingThisAccount == null)
            {
                serversResponsibleForCachingThisAccount =
                    GetServersResponsibleForCachingAnAccount(attempt.UsernameOrAccountId);
            }
            await Task.WhenAll(serversResponsibleForCachingThisAccount.Select(server =>
                RestClientHelper.PostAsync(server.Uri,
                    "/api/UserAccount/" + Uri.EscapeUriString(attempt.UsernameOrAccountId),
                    new Object[]
                    {
                        new KeyValuePair<string, LoginAttempt>("attempt", attempt),
                        new KeyValuePair<string, bool>("onlyUpdateTheInMemoryCacheOfTheAccount", true)
                    }, timeout, cancellationToken)).ToArray());
        }

        public void UpdateForNewLoginAttemptCacheOnlyInBackground(LoginAttempt attempt,
            List<RemoteHost> serversResponsibleForCachingThisAccount = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task.Run(() => UpdateForNewLoginAttemptCacheOnlyAsync(
                attempt, serversResponsibleForCachingThisAccount, timeout, cancellationToken),
                cancellationToken);
        }
    }
}
