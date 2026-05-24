import {kvLocalProvider} from "./providers/kvLocalProvider";
import {kvServerProvider} from "./providers/kvServerProvider";
import {getSetting, setSetting} from "./settings";
import {getEffectiveServerUrl} from "./serverRotation";

export const formatResponse = (data) => data;

export const formatError = (message, code = "UNKNOWN_ERROR") => ({
  success: false,
  error: {code, message},
});

// Cache key prefix to avoid collision with kv-local mode data
const CACHE_PREFIX = "_cache:";

function isServerError(result) {
  return result && result.success === false;
}

function isNetworkError(result) {
  return isServerError(result) && result.error?.code === "NETWORK_ERROR";
}

// --- Sync manager: flushes queued writes when back online ---

let _onlineHandler = null;
let _flushing = false;

async function flushSyncQueue() {
  if (_flushing) return;
  _flushing = true;
  try {
    const queueResult = await kvLocalProvider.getSyncQueue();
    if (queueResult.success === false || !Array.isArray(queueResult) || queueResult.length === 0) {
      return;
    }
    for (const entry of queueResult) {
      try {
        const result = await kvServerProvider.saveData(entry.key, entry.data);
        if (result.success !== false) {
          await kvLocalProvider.removeFromSyncQueue(entry.key);
        }
      } catch {
        // If a single item fails, stop — will retry on next online event
        break;
      }
    }
  } finally {
    _flushing = false;
  }
}

export const syncManager = {
  init() {
    if (_onlineHandler) return;
    _onlineHandler = () => flushSyncQueue();
    window.addEventListener("online", _onlineHandler);
    // Attempt flush on startup in case items were queued before last exit
    if (navigator.onLine) flushSyncQueue();
  },
  destroy() {
    if (_onlineHandler) {
      window.removeEventListener("online", _onlineHandler);
      _onlineHandler = null;
    }
  },
  flushNow: flushSyncQueue,
};

// Helper: check if we should use the server provider
function useServerProvider() {
  const provider = getSetting("server.provider");
  return provider === "kv-server" || provider === "classworkscloud";
}

// Main data provider with simplified API
export default {
  // Provider API methods
  loadData: async (key) => {
    if (!useServerProvider()) {
      return kvLocalProvider.loadData(key);
    }

    // Server mode: network-first with cache fallback
    const result = await kvServerProvider.loadData(key);

    if (!isNetworkError(result)) {
      // Success or non-network error (e.g. NOT_FOUND) — cache on success
      if (result.success !== false) {
        kvLocalProvider.saveData(CACHE_PREFIX + key, result);
      }
      return result;
    }

    // Network error — try local cache
    const cached = await kvLocalProvider.loadData(CACHE_PREFIX + key);
    if (cached.success !== false) {
      return {...cached, fromCache: true};
    }

    return result;
  },

  saveData: async (key, data) => {
    if (!useServerProvider()) {
      return kvLocalProvider.saveData(key, data);
    }

    // Server mode: write-through — persist locally first
    await kvLocalProvider.saveData(CACHE_PREFIX + key, data);

    const result = await kvServerProvider.saveData(key, data);

    if (result.success !== false) {
      // Server save succeeded — remove from sync queue if present
      await kvLocalProvider.removeFromSyncQueue(key);
      return result;
    }

    // Server save failed — queue for later sync
    await kvLocalProvider.addToSyncQueue({key, data, timestamp: Date.now()});
    return {success: true, queuedForSync: true};
  },

  loadKeys: async (options = {}) => {
    if (!useServerProvider()) {
      return kvLocalProvider.loadKeys(options);
    }

    const result = await kvServerProvider.loadKeys(options);

    if (!isNetworkError(result)) {
      return result;
    }

    // Network error — fall back to local cache keys
    return kvLocalProvider.loadKeys(options);
  },

  async getKeyCloudUrl(key, options = {}) {
    const {
      migrateFromLocal = true,
      autoConfigureCloud = true
    } = options;

    try {
      const provider = getSetting("server.provider");
      let serverUrl;

      // Use effective server URL for classworkscloud provider
      if (provider === "classworkscloud") {
        serverUrl = getEffectiveServerUrl();
      } else {
        serverUrl = getSetting("server.domain");
      }

      let siteKey = getSetting("server.siteKey");
      const machineId = getSetting("device.uuid");
      let configured = false;

      // 检查云端配置是否为空或错误，如果是则使用默认配置
      if (!serverUrl || !machineId) {
        if (autoConfigureCloud) {
          // 使用classworksCloudDefaults配置
          const classworksCloudDefaults = {
            "server.domain": import.meta.env.VITE_DEFAULT_KV_SERVER || "https://kv-service.houlang.cloud",
            "server.siteKey": "",
          };

          if (!serverUrl) {
            setSetting("server.domain", classworksCloudDefaults["server.domain"]);
            serverUrl = classworksCloudDefaults["server.domain"];
            configured = true;
          }

          if (!siteKey) {
            setSetting("server.siteKey", classworksCloudDefaults["server.siteKey"]);
            siteKey = classworksCloudDefaults["server.siteKey"];
          }

          // 设置provider为classworkscloud
          setSetting("server.provider", "classworkscloud");
          // Get effective URL after setting provider
          serverUrl = getEffectiveServerUrl();
        } else {
          return formatError("云端配置无效，请检查服务器域名和设备UUID", "CONFIG_ERROR");
        }
      }

      let migrated = false;

      // 如果需要迁移本地数据到云端
      if (migrateFromLocal) {
        try {
          // 尝试从本地读取数据
          const localData = await kvLocalProvider.loadData(key);

          // 如果本地有数据且不是错误响应
          if (localData && localData.success !== false) {
            // 检查云端是否已有数据
            const cloudData = await kvServerProvider.loadData(key);

            // 如果云端没有数据，则迁移本地数据
            if (cloudData && cloudData.success === false && cloudData.error?.code === "NOT_FOUND") {
              const saveResult = await kvServerProvider.saveData(key, localData);
              if (saveResult && saveResult.success !== false) {
                migrated = true;
                console.log(`已成功将键 ${key} 的数据从本地迁移到云端`);
              }
            }
          }
        } catch (error) {
          console.warn(`迁移键 ${key} 的数据时出错:`, error);
          // 迁移失败不影响URL生成，继续执行
        }
      }
      // 获取认证token
      const authtoken = getSetting("server.kvToken");
      // 构建云端访问URL
      let url = `${serverUrl}/kv/${key}?token=${authtoken}`;


      return {
        success: true,
        url,
        migrated,
        configured
      };

    } catch (error) {
      console.error('获取键云端地址时出错:', error);
      return formatError(
        error.message || "获取键云端地址失败",
        "CLOUD_URL_ERROR"
      );
    }
  },
};


export const ErrorCodes = {
  NOT_FOUND: "数据不存在",
  NETWORK_ERROR: "网络连接失败",
  SERVER_ERROR: "服务器错误",
  SAVE_ERROR: "保存失败",
  CONFIG_ERROR: "配置错误",
  PERMISSION_DENIED: "无权限访问",
  UNAUTHORIZED: "认证失败",
  CLOUD_URL_ERROR: "云端地址获取失败",
  UNKNOWN_ERROR: "未知错误",
};
