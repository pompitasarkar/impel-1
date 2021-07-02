﻿using System;
using System.ComponentModel;
using System.Numerics;

using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace ImpelSC
{
    [DisplayName("Impel.ImpelSCv0.1.40")]
    [ManifestExtra("Author", "Kinshuk Kar, Pompita Sarkar")]
    [ManifestExtra("Email", "kinshuk89@gmail.com")]
    [ManifestExtra("Description", "A novel motivation mechanism to assist people in getting fitter with social and financial rewards")]
    [ContractPermission("*", "onNEP17Payment")]
    [ContractTrust("0xd2a4cff31913016155e38e474a2c06d08be276cf")]
    
    public class ImpelSCContract : SmartContract
    {
        static readonly ImpelStorage contractData = new ImpelStorage();
        private static Transaction Tx => (Transaction) Runtime.ScriptContainer;

        [DisplayName("_deploy")]
        public static void Deploy(object data, bool update) {
            if (!update)
            {
                initialize();
            }
        }

        private static void initialize() {

            contractData.PutOwner((ByteString) Tx.Sender);
            contractData.ResetLastChallengeId();
            BigInteger newChallengeId = contractData.GetAndIncrementLastChallengeId();
            Challenge dummyChallenge = Challenge.getTestChallenge();
            contractData.PutChallenge(newChallengeId, dummyChallenge);
        }

        public static void UpdateContract(ByteString nefFile, string manifest) {
            ByteString owner = contractData.GetOwner();
            if (!Tx.Sender.Equals(owner))
            {
                throw new Exception("Only the contract owner can do this");
            }
            ContractManagement.Update(nefFile, manifest, null);
        }

        public static void DestroyContract(ByteString nefFile, string manifest) {
            ByteString owner = contractData.GetOwner();
            if (!Tx.Sender.Equals(owner))
            {
                throw new Exception("Only the contract owner can do this");
            }
            ContractManagement.Destroy();
        }

        public static string ToAddress(ByteString sender) {
            var address = StdLib.Base58Encode("5" + sender);
            Runtime.Log(address);
            return (address);
        }

        public static void RegisterUser(string username) {
            User newUser = new User(username);
            contractData.PutUser(ToAddress((ByteString) Tx.Sender), newUser);
        }

        public static User RetrieveUser() {
            return RetrieveUserByAddress(ToAddress((ByteString) Tx.Sender));
        }

        public static User RetrieveUserByAddress(string address) {
            return contractData.GetUser(address);
        }

        public List<UserCommit> GetSubscribedEntriesForChallenge(BigInteger challengeId) {
            return contractData.GetSubscribedEntriesForChallenge(challengeId);
        }

        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object[] data) {

            if (!Runtime.CheckWitness(from)) throw new Exception("Check your signature.");

            if (Runtime.CallingScriptHash == GAS.Hash) {
                if (data.Length == 2 && (string)data[0] == "join_challenge") {
                    BigInteger challengeId = (BigInteger) data[1];
                    contractData.AddUserChallengeRecord(challengeId, ToAddress((ByteString) from), amount);
                }
            }
        }
    }

    class ImpelStorage
    {
        const string IMPELSC_STORAGE_CORE_DATA = "A*";
        const string IMPELSC_STORAGE_USERS = "B*";
        const string IMPELSC_STORAGE_CHALLENGES = "C*";
        const string IMPELSC_STORAGE_USER_CHALLENGES = "D*";
        
        readonly StorageMap dappData;
        readonly StorageMap usersMap;
        readonly StorageMap challengesMap;
        readonly StorageMap userChallengeMapping;

        public ImpelStorage() {
            dappData = new StorageMap(Storage.CurrentContext, IMPELSC_STORAGE_CORE_DATA);
            usersMap = new StorageMap(Storage.CurrentContext, IMPELSC_STORAGE_USERS);
            challengesMap = new StorageMap(Storage.CurrentContext, IMPELSC_STORAGE_CHALLENGES);
            userChallengeMapping = new StorageMap(Storage.CurrentContext, IMPELSC_STORAGE_USER_CHALLENGES);
        }

        public User GetUser(string userAccount) {
            string userJSON = usersMap.Get(userAccount);
            if (userJSON == null || userJSON == "") {
                return new User("");
            }
            return User.Deserialize(userJSON);
        }

        public void PutUser(string userKey, User user) => usersMap.Put(userKey, User.Serialize(user));
        
        public string GetOwner() => (ByteString)dappData.Get("Owner") ?? "";
        
        public void PutOwner(ByteString owner) => dappData.Put("Owner", (ByteString)owner);
        
        public void ResetLastChallengeId() => dappData.Put("LastChallengeId", 1);

        public BigInteger GetAndIncrementLastChallengeId() {
            BigInteger lastChallengeId = (BigInteger) dappData.Get("LastChallengeId");
            dappData.Put("LastChallengeId", lastChallengeId + 1);
            return lastChallengeId;
        }

        public Challenge GetChallenge(BigInteger challengeId) {
            string challengeJSON = challengesMap.Get((ByteString)challengeId);
            if (challengeJSON == null || challengeJSON == "") {
                return null;
            }
            return Challenge.Deserialize(challengeJSON);
        }

        public void PutChallenge(BigInteger challengeId, Challenge challenge) {
            challengesMap.Put( (ByteString)challengeId, Challenge.Serialize(challenge));
        }

        public void AddUserChallengeRecord(BigInteger challengeId, string userKey, BigInteger amount) {

            UserCommit userChallengeRecord = new UserCommit(userKey, (int)amount);
            string entry = StdLib.JsonSerialize(userChallengeRecord);
            string recordKey = "c#" + challengeId + userKey;
            userChallengeMapping.Put(recordKey, entry);
        }

        public UserCommit GetChallengeEntry(string key) {
            return (UserCommit) StdLib.JsonDeserialize(userChallengeMapping.Get(key));

        }
        public List<UserCommit> GetSubscribedEntriesForChallenge(BigInteger challengeId) {
            List<UserCommit> challengeEntries = new List<UserCommit>();
            var iterator = userChallengeMapping.Find("c#" + challengeId, FindOptions.KeysOnly | FindOptions.RemovePrefix);
            while(iterator.Next()) {
                string key = "c#" + challengeId + (string)iterator.Value;
                challengeEntries.Add(GetChallengeEntry(key));
            }

            return challengeEntries;
        }
    }

    public class User {
        public string username;
        public User(string name) {
            username = name;
        }

        public static string Serialize(User user) {
            return StdLib.JsonSerialize(user);
        }

        public static User Deserialize(string json) {
            return (User) StdLib.JsonDeserialize(json);
        }

    }

    public class UserCommit {
        
        public enum UserChallengeState {
            DataNotSubmitted,
            DataSubmitted,
            DataSubmittedQualified,
            DataSubmittedNotQualified
        }
        public string userKey;
        public int commitAmount;

        public UserChallengeState state;

        public UserCommit(string key, int amount) {
            userKey = key;
            commitAmount = amount;
            state = UserChallengeState.DataNotSubmitted;
        }
    }

    class Challenge
    {
        public enum ChallengeState {
            ChallengeStateNotStarted,
            ChallengeStateActive,
            ChallengeStateCompleted,
            ChallengeStateEvaluationCompleted
        }

        public enum ChallengeActivityType {
            ChallengeActivityTypeWalkRun
        }

        public enum ChallengeType {
            ChallengeTypeMax
        }
        public string challengeTitle;
        public ulong challengeStartTime;
        public ulong challengeEndTime;
        public ulong challengeEvaluationTime;
        public ChallengeState challengeState;
        public ChallengeActivityType challengeActivityType;
        public ChallengeType challengeType;
        public BigInteger challengeValue;

        public Challenge(string title, ulong startTime, ulong endTime, ulong evaluationTime, ChallengeActivityType activityType, ChallengeType type, BigInteger value) {
            challengeTitle = title;
            challengeStartTime = startTime;
            challengeEndTime = endTime;
            challengeEvaluationTime = evaluationTime;
            challengeState = ChallengeState.ChallengeStateNotStarted;
            challengeActivityType = activityType;
            challengeType = type;
            challengeValue = value;
        }

        public static string Serialize(Challenge challenge) {
            return StdLib.JsonSerialize(challenge);
        }

        public static Challenge Deserialize(string json) {
            return (Challenge) StdLib.JsonDeserialize(json);
        }

        public static Challenge getTestChallenge()  {
            Challenge newChallenge = new Challenge("June 5K Challenge", 1624559400000, 1624991400000, 1625164200000, ChallengeActivityType.ChallengeActivityTypeWalkRun, ChallengeType.ChallengeTypeMax, 5);
            return newChallenge;
        }
    }
}