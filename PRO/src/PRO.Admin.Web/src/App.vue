<template>
  <router-view />
</template>

<script setup>
</script>

<style>
:root {
  --el-color-primary: #0071e3 !important;
  --el-color-primary-light-3: #4099f0 !important;
  --el-color-primary-light-5: #7fbefa !important;
  --el-color-primary-light-7: #bfdeff !important;
  --el-color-primary-light-8: #dcf0ff !important;
  --el-color-primary-light-9: #f0f7ff !important;
  --el-color-primary-dark-2: #0056b3 !important;

  --el-color-success: #34c759 !important;
  --el-color-success-light-3: #70d98a !important;
  --el-color-success-light-5: #9ae5ac !important;
  --el-color-success-light-9: #f2fcf5 !important;

  --el-color-warning: #ff9500 !important;
  --el-color-warning-light-3: #ffb54d !important;
  --el-color-warning-light-5: #ffca80 !important;
  --el-color-warning-light-9: #fff9f0 !important;

  --el-color-danger: #ff3b30 !important;
  --el-color-danger-light-3: #ff756e !important;
  --el-color-danger-light-5: #ff9d99 !important;
  --el-color-danger-light-9: #fff5f5 !important;

  --el-color-info: #8e8e93 !important;
  --el-color-info-light-3: #b0b0b5 !important;
  --el-color-info-light-5: #c7c7cc !important;
  --el-color-info-light-9: #f2f2f7 !important;

  --el-text-color-primary: #1d1d1f !important;
  --el-text-color-regular: #424245 !important;
  --el-text-color-secondary: #86868b !important;
  --el-text-color-placeholder: #b6b6bb !important;

  --el-border-radius-base: 10px !important;
  --el-border-radius-small: 6px !important;
  --el-border-radius-large: 16px !important;

  --el-border-color: rgba(0, 0, 0, 0.08) !important;
  --el-border-color-light: rgba(0, 0, 0, 0.05) !important;
  --el-border-color-lighter: rgba(0, 0, 0, 0.03) !important;
  
  --el-box-shadow-light: 0 4px 30px rgba(0, 0, 0, 0.03) !important;
  --el-box-shadow-dark: 0 12px 40px rgba(0, 0, 0, 0.08) !important;

  --apple-ease: cubic-bezier(0.16, 1, 0.3, 1);
  --apple-transition: all 0.3s cubic-bezier(0.16, 1, 0.3, 1);
}

* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

html, body, #app {
  height: 100%;
  background-color: #f5f5f7;
  color: #1d1d1f;
  font-family: -apple-system, BlinkMacSystemFont, "SF Pro Text", "SF Pro Icons", "Helvetica Neue", Helvetica, Arial, sans-serif;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
}

::-webkit-scrollbar {
  width: 6px;
  height: 6px;
}
::-webkit-scrollbar-track {
  background: transparent;
}
::-webkit-scrollbar-thumb {
  background: rgba(0, 0, 0, 0.10);
  border-radius: 3px;
  transition: background 0.2s;
}
::-webkit-scrollbar-thumb:hover {
  background: rgba(0, 0, 0, 0.20);
}
::-webkit-scrollbar-corner {
  background: transparent;
}

.el-card {
  border: 1px solid rgba(0, 0, 0, 0.05) !important;
  border-radius: 18px !important;
  background: rgba(255, 255, 255, 0.85) !important;
  backdrop-filter: blur(20px) saturate(190%) !important;
  box-shadow: 0 4px 30px rgba(0, 0, 0, 0.02) !important;
  transition: transform 0.4s var(--apple-ease), box-shadow 0.4s var(--apple-ease) !important;
}

.el-card.is-hover-shadow:hover {
  transform: translateY(-2px);
  box-shadow: 0 12px 40px rgba(0, 0, 0, 0.05) !important;
}

.el-input__wrapper {
  background-color: rgba(0, 0, 0, 0.02) !important;
  box-shadow: inset 0 0 0 1px rgba(0, 0, 0, 0.08) !important;
  border-radius: 10px !important;
  transition: var(--apple-transition) !important;
}
.el-input__wrapper.is-focus {
  background-color: #fff !important;
  box-shadow: 0 0 0 3px rgba(0, 113, 227, 0.15), inset 0 0 0 1px #0071e3 !important;
}

.el-button {
  border-radius: 10px !important;
  font-weight: 500 !important;
  transition: var(--apple-transition) !important;
}
.el-button--primary {
  background: #0071e3 !important;
  border-color: #0071e3 !important;
}
.el-button--primary:hover, .el-button--primary:focus {
  background: #147ce5 !important;
  border-color: #147ce5 !important;
}
.el-button:active {
  transform: scale(0.96);
}

.el-tag {
  border-radius: 6px !important;
  border: none !important;
  font-weight: 500 !important;
}

.el-table {
  --el-table-border-color: rgba(0, 0, 0, 0.04) !important;
  --el-table-header-bg-color: transparent !important;
  border-radius: 12px !important;
  overflow: hidden !important;
  background: transparent !important;
}
.el-table tr {
  background-color: transparent !important;
}
.el-table th.el-table__cell {
  font-weight: 600 !important;
  color: #86868b !important;
  border-bottom: 1px solid rgba(0, 0, 0, 0.06) !important;
}
.el-table td.el-table__cell {
  border-bottom: 1px solid rgba(0, 0, 0, 0.04) !important;
}

.el-date-editor .el-range-separator {
  color: #424245 !important;
}
</style>