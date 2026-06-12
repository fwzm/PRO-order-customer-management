// PRO PWA Service Worker - 离线缓存 + 草稿自动保存 + 网络恢复同步
const CACHE_NAME = 'pro-app-v1';
const DRAFT_CACHE = 'pro-drafts-v1';
const SYNC_QUEUE = 'pro-sync-queue';
const STATIC_ASSETS = [
  '/',
  '/index.html',
  '/manifest.json',
  '/src/main.js',
  '/src/App.vue'
];

// 安装 - 缓存静态资源
self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => {
      return cache.addAll(STATIC_ASSETS);
    })
  );
  self.skipWaiting();
});

// 激活 - 清理旧缓存
self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) => {
      return Promise.all(
        keys.filter(key => key !== CACHE_NAME && key !== DRAFT_CACHE)
          .map(key => caches.delete(key))
      );
    })
  );
  self.clients.claim();
});

// 拦截请求 - 网络优先策略
self.addEventListener('fetch', (event) => {
  // API请求：网络优先，失败时返回缓存（GET only）
  if (event.request.url.includes('/api/')) {
    if (event.request.method === 'GET') {
      event.respondWith(networkFirst(event.request));
    }
    return;
  }

  // 静态资源：缓存优先
  event.respondWith(cacheFirst(event.request));
});

// 网络优先策略
async function networkFirst(request) {
  try {
    const response = await fetch(request);
    const cache = await caches.open(CACHE_NAME);
    cache.put(request, response.clone());
    return response;
  } catch (e) {
    const cached = await caches.match(request);
    return cached || new Response(JSON.stringify({ success: false, message: '离线状态，数据可能不是最新' }), {
      headers: { 'Content-Type': 'application/json' }
    });
  }
}

// 缓存优先策略
async function cacheFirst(request) {
  const cached = await caches.match(request);
  if (cached) return cached;
  try {
    const response = await fetch(request);
    const cache = await caches.open(CACHE_NAME);
    cache.put(request, response.clone());
    return response;
  } catch (e) {
    return new Response('', { status: 408 });
  }
}

// 后台同步 - 网络恢复后提交草稿
self.addEventListener('sync', (event) => {
  if (event.tag === 'sync-drafts') {
    event.waitUntil(syncDrafts());
  }
});

async function syncDrafts() {
  const drafts = await getDrafts();
  for (const draft of drafts) {
    try {
      const response = await fetch('/api/orders', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(draft.data)
      });
      if (response.ok) {
        await removeDraft(draft.id);
      }
    } catch (e) {
      console.warn('草稿同步失败，将在下次同步重试:', e);
    }
  }
}

// 消息处理 - 接收主线程的草稿保存请求
self.addEventListener('message', (event) => {
  if (event.data.type === 'SAVE_DRAFT') {
    saveDraft(event.data.draft);
  } else if (event.data.type === 'GET_DRAFTS') {
    getDrafts().then(drafts => event.ports[0].postMessage({ drafts }));
  } else if (event.data.type === 'CLEAR_DRAFTS') {
    clearDrafts();
  }
});

// 草稿存储（IndexedDB 简化版，使用 Cache API）
async function saveDraft(draft) {
  const cache = await caches.open(DRAFT_CACHE);
  await cache.put(`draft-${draft.id}`, new Response(JSON.stringify(draft)));
}

async function getDrafts() {
  const cache = await caches.open(DRAFT_CACHE);
  const keys = await cache.keys();
  const drafts = [];
  for (const key of keys) {
    const response = await cache.match(key);
    if (response) {
      drafts.push(await response.json());
    }
  }
  return drafts;
}

async function removeDraft(id) {
  const cache = await caches.open(DRAFT_CACHE);
  await cache.delete(`draft-${id}`);
}

async function clearDrafts() {
  const cache = await caches.open(DRAFT_CACHE);
  const keys = await cache.keys();
  for (const key of keys) {
    await cache.delete(key);
  }
}
