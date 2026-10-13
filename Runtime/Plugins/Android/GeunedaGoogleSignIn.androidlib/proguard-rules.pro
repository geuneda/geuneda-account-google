# C# 에서 JNI 이름으로 부르는 클래스와 AndroidJavaProxy 가 구현하는 인터페이스
-keep class com.geuneda.account.google.GeunedaGoogleSignIn { public static *; }
-keep interface com.geuneda.account.google.GeunedaGoogleSignInCallback { *; }

# Credential Manager 가 리플렉션으로 찾는 Play 서비스 구현(androidx.credentials 문서 권장)
-if class androidx.credentials.CredentialManager
-keep class androidx.credentials.playservices.** { *; }
