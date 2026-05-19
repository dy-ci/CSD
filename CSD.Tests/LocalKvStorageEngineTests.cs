using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CSD.Services;

namespace CSD.Tests
{
    [TestClass]
    public class LocalKvStorageEngineTests
    {
        [TestInitialize]
        public void Setup()
        {
            LocalKvStorageEngine.IsPrivacyMode = false;
            LocalKvStorageEngine.ClearAll();
        }

        [TestCleanup]
        public void Cleanup()
        {
            LocalKvStorageEngine.ClearAll();
        }

        [TestMethod]
        public async Task Test_WriteAndReadLocalAsync_Success()
        {
            // Arrange
            string key = "test-key-1";
            string testData = "{\"message\": \"hello local storage\"}";

            // Act
            await LocalKvStorageEngine.WriteLocalAsync(key, testData);
            var result = await LocalKvStorageEngine.ReadLocalAsync(key);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(testData, result);
        }

        [TestMethod]
        public async Task Test_HandleRequestAsync_Get_Post_Delete()
        {
            // Arrange
            string path = "/kv/test-key-2";
            string testData = "{\"status\": \"ok\"}";

            // Act & Assert - POST
            var postResponse = await LocalKvStorageEngine.HandleRequestAsync(HttpMethod.Post, path, testData);
            Assert.AreEqual(HttpStatusCode.OK, postResponse.StatusCode);

            // Act & Assert - GET
            var getResponse = await LocalKvStorageEngine.HandleRequestAsync(HttpMethod.Get, path, null);
            Assert.AreEqual(HttpStatusCode.OK, getResponse.StatusCode);
            var getContent = await getResponse.Content.ReadAsStringAsync();
            Assert.AreEqual(testData, getContent);

            // Act & Assert - DELETE
            var deleteResponse = await LocalKvStorageEngine.HandleRequestAsync(HttpMethod.Delete, path, null);
            Assert.AreEqual(HttpStatusCode.OK, deleteResponse.StatusCode);

            // Act & Assert - GET after DELETE
            var getResponseAfterDelete = await LocalKvStorageEngine.HandleRequestAsync(HttpMethod.Get, path, null);
            Assert.AreEqual(HttpStatusCode.NotFound, getResponseAfterDelete.StatusCode);
        }

        [TestMethod]
        public async Task Test_PrivacyMode_MemoryStorage()
        {
            // Arrange
            LocalKvStorageEngine.IsPrivacyMode = true;
            string key = "privacy-key";
            string testData = "{\"secret\": true}";

            // Act
            await LocalKvStorageEngine.WriteLocalAsync(key, testData);
            var result = await LocalKvStorageEngine.ReadLocalAsync(key);

            // Assert
            Assert.AreEqual(testData, result);
            
            // Ensure no file was created
            var filePath = Path.Combine(LocalKvStorageEngine.LocalStorageDirectory, $"{key}.json");
            Assert.IsFalse(File.Exists(filePath));
        }

        [TestMethod]
        public async Task Test_QuotaExceededError_ThrowsException()
        {
            // Arrange
            LocalKvStorageEngine.IsPrivacyMode = true; // Use privacy mode for easier quota simulation
            string key = "huge-data";
            // Create a huge string to exceed 50MB
            // Actually, creating 50MB string might cause OutOfMemory in test, let's just assert the exception logic
            // Since we hardcoded MaxLocalStorageSize to 50MB, we can try to exceed it by writing 51MB.
            int sizeToExceed = 51 * 1024 * 1024;
            string hugeData = new string('A', sizeToExceed);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await LocalKvStorageEngine.WriteLocalAsync(key, hugeData);
            }, "QuotaExceededError: Local storage space exceeded limit in privacy mode.");
        }
        
        [TestMethod]
        public async Task Test_DataFormatConsistency()
        {
            // Arrange
            string path = "/kv/classworks-data-20231012";
            var payload = new System.Collections.Generic.Dictionary<string, object>
            {
                ["homework"] = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["Math"] = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["content"] = "Do exercise 1"
                    }
                }
            };
            string json = JsonSerializer.Serialize(payload);

            // Act
            await LocalKvStorageEngine.HandleRequestAsync(HttpMethod.Post, path, json);
            var getResponse = await LocalKvStorageEngine.HandleRequestAsync(HttpMethod.Get, path, null);
            var resultJson = await getResponse.Content.ReadAsStringAsync();

            // Assert
            using var doc = JsonDocument.Parse(resultJson);
            Assert.IsTrue(doc.RootElement.TryGetProperty("homework", out var hw));
            Assert.IsTrue(hw.TryGetProperty("Math", out var math));
            Assert.IsTrue(math.TryGetProperty("content", out var content));
            Assert.AreEqual("Do exercise 1", content.GetString());
        }
    }
}