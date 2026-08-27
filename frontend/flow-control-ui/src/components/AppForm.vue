<template>
  <div class="form">
    <slot name="header"></slot>
    <form>
      <slot></slot>
      <slot name="footer"></slot>
    </form>
  </div>
</template>
