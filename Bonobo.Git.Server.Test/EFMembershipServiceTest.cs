﻿using System;
using System.Linq;
using System.Security.Cryptography;
using Bonobo.Git.Server.Data;
using Bonobo.Git.Server.Data.Update;
using Bonobo.Git.Server.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bonobo.Git.Server.Test
{
    [TestClass]
    public class EFMembershipServiceTest : MembershipServiceTestBase
    {
        SqliteTestConnection _connection;

        [TestInitialize]
        public void Initialize()
        {
            _connection = new SqliteTestConnection();
            _service = new EFMembershipService(MakeContext);
            new AutomaticUpdater().RunWithContext(MakeContext());
        }

        BonoboGitServerContext MakeContext()
        {
            return _connection.GetContext();
        }

        [TestMethod]
        public void UpdatesCanBeRunOnAlreadyUpdatedDatabase()
        {
            // Run all the updates again - this should be completely harmless
            new AutomaticUpdater().RunWithContext(MakeContext());
        }

        [TestMethod]
        public void NewDatabaseContainsJustAdminUser()
        {
            var admin = _service.GetAllUsers().Single();
            Assert.AreEqual("admin", admin.Name);
        }

        [TestMethod]
        public void NewAdminUserHasCorrectPassword()
        {
            Assert.AreEqual(ValidationResult.Success, _service.ValidateUser("admin", "admin"));
        }

        [TestMethod]
        public void PasswordsAreCaseSensitive()
        {
            Assert.AreEqual(ValidationResult.Failure, _service.ValidateUser("admin", "Admin"));
        }

        [TestMethod]
        public void TestThatValidatingAUserWithDeprecatedHashUpgradesTheirPassword()
        {
            ForceInDeprecatedHash("admin", "adminpassword");
            var startingHash = GetRawUser("admin").Password;
            var startingSalt = GetPasswordSalt("admin");

            // Validation should cause the hash to be upgraded
            Assert.AreEqual(ValidationResult.Success, _service.ValidateUser("Admin", "adminpassword"));

            // We should have different salt, and different password
            Assert.AreNotEqual(startingSalt, GetPasswordSalt("admin"));
            Assert.AreNotEqual(startingHash, GetRawUser("admin").Password);
        }

        [TestMethod]
        public void TestThatFailingToValidateAUserWithDeprecatedHashDoesNotUpgradeTheirPassword()
        {
            ForceInDeprecatedHash("admin", "adminpassword");
            var startingHash = GetRawUser("admin").Password;
            var startingSalt = GetPasswordSalt("admin");

            // Validation should cause the hash to be upgraded
            Assert.AreEqual(ValidationResult.Failure, _service.ValidateUser("Admin", "adminpasswordWrong"));

            // We should have different salt, and different password
            Assert.AreEqual(startingSalt, GetPasswordSalt("admin"));
            Assert.AreEqual(startingHash, GetRawUser("admin").Password);
        }

        // This is ignored for the moment, because I haven't enabled the forced upgrade stuff
        [TestMethod, Ignore]
        public void TestThatValidatingAUserWithOldStyleSaltUpgradesTheirSalt()
        {
            // By default, the start admin user will have old-style salt (just the username)
            Assert.AreEqual("admin", GetPasswordSalt("admin"));

            Assert.AreEqual(ValidationResult.Success, _service.ValidateUser("admin", "admin"));

            // Now, the salt should have changed
            Assert.AreNotEqual("admin", GetPasswordSalt("admin"));
        }

        User GetRawUser(string username)
        {
            using (var context = MakeContext())
            {
                username = username.ToLower();
                return context.Users.First(u => u.Username == username);
            }
        }

        string GetPasswordSalt(string username)
        {
            return GetRawUser(username).PasswordSalt;
        }

        void ForceInDeprecatedHash(string username, string password)
        {
            using (var context = MakeContext())
            {
                username = username.ToLower();
                var user = context.Users.First(u => u.Username == username);

                using (var hashProvider = new MD5CryptoServiceProvider())
                {
                    var data = System.Text.Encoding.UTF8.GetBytes(password);
                    data = hashProvider.ComputeHash(data);
                    user.Password = BitConverter.ToString(data).Replace("-", "");
                    user.PasswordSalt = "";
                }
                context.SaveChanges();
            }
        }
    }
}