/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_HIDDEN_FLOW_NODE_KINDS?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

declare module 'monaco-editor/esm/vs/editor/editor.main' {
  export * from 'monaco-editor';
}
