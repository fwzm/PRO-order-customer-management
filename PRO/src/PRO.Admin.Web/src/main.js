import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'

// Element Plus 按需引入：unplugin-vue-components 自动处理组件导入，
// unplugin-auto-import 自动处理 API（ElMessage、ElLoading 等）。
// 仅需引入样式即可。
import 'element-plus/dist/index.css'

const app = createApp(App)

app.use(createPinia())
app.use(router)
app.mount('#app')
