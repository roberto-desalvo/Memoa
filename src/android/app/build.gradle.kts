// Trusted Web Activity: apre a schermo intero la PWA distribuita su Static Web Apps.
// Host e versione arrivano dalla pipeline (android.yml): -PwebHost=<host> -PversionCode=<n>.
// Firma dalle variabili d'ambiente ANDROID_KEYSTORE_PATH, ANDROID_KEYSTORE_PASSWORD, ANDROID_KEY_ALIAS, ANDROID_KEY_PASSWORD.
plugins {
    id("com.android.application")
}

val webHost = providers.gradleProperty("webHost").orNull
    ?: error("Manca l'host della Static Web App: -PwebHost=<host>")
val appVersionCode = providers.gradleProperty("versionCode").orNull?.toInt() ?: 1
val keystorePath = System.getenv("ANDROID_KEYSTORE_PATH")

android {
    namespace = "app.memoa"
    compileSdk = 36

    defaultConfig {
        applicationId = "app.memoa"
        minSdk = 26
        targetSdk = 36
        versionCode = appVersionCode
        versionName = "1.0.$appVersionCode"

        val launchUrl = "https://$webHost/"
        manifestPlaceholders["webHost"] = webHost
        resValue("string", "launchUrl", launchUrl)
        // Collegamento con /.well-known/assetlinks.json del sito: senza, Chrome mostra la barra dell'URL.
        resValue(
            "string",
            "assetStatements",
            """[{"relation":["delegate_permission/common.handle_all_urls"],"target":{"namespace":"web","site":"$launchUrl"}}]"""
                .replace("\"", "\\\""),
        )
    }

    flavorDimensions += "environment"
    productFlavors {
        create("dev") {
            dimension = "environment"
            applicationIdSuffix = ".dev"
            resValue("string", "appName", "Memoa Dev")
        }
        create("prod") {
            dimension = "environment"
            resValue("string", "appName", "Memoa")
        }
    }

    signingConfigs {
        if (keystorePath != null) {
            create("release") {
                storeFile = file(keystorePath)
                storePassword = System.getenv("ANDROID_KEYSTORE_PASSWORD")
                keyAlias = System.getenv("ANDROID_KEY_ALIAS")
                keyPassword = System.getenv("ANDROID_KEY_PASSWORD")
            }
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            signingConfig = signingConfigs.findByName("release")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
}

dependencies {
    implementation("com.google.androidbrowserhelper:androidbrowserhelper:2.7.4")
}
