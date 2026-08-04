/* Enters the portable controller startup path from the ESP-IDF runtime. */
void controller_main(void);

/* Delegates startup so the platform entry remains free of controller logic. */
void platform_main(void)
{
    controller_main();
}
