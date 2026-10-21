# Restaurant POS 1.1.2

برنامج كاشير مطاعم عربي لسطح مكتب Windows، مكتوب بـ C# وWinForms على .NET Framework 4.8.

## التشغيل والصلاحيات

- تسجيل الدخول قبل شاشة الكاشير، وإنشاء حساب المدير عند التثبيت الأول.
- المدير: الإعدادات وتعديل الأوردر المكتمل وإعادة الطباعة.
- الموظف: إعادة الطباعة واستكمال أوردرات الانتظار؛ أزرار الإعدادات وتعديل الأوردر المكتمل لا تظهر.
- قاعدة SQLite مستقلة تُنشأ تلقائيًا داخل `%LOCALAPPDATA%\RestaurantPos`؛ لا توجد بيانات عميل ضمن هذا المستودع.
- أصناف بعرض متجاوب وتمرير يعيد استخدام البطاقات، مخزون وتقارير ونسخ احتياطية وطباعة إيصالات عربية RAW/ESC-POS.

## بناء البرنامج

على Windows، ثبّت .NET SDK مع دعم بناء مشاريع .NET Framework، ثم:

```powershell
dotnet build RestaurantPos.sln -c Release -t:Rebuild -p:PlatformTarget=AnyCPU
```

يستعيد NuGet الحزم ومراجع .NET Framework تلقائيًا. ملفات التشغيل تظهر في `src/RestaurantPos/bin/Release/net48/`. التشغيل يحتاج .NET Framework 4.8؛ لا يحتاج Python أو SDK.

## بناء المثبت

ثبّت Inno Setup 6.4.3، ثم مرر مسار المترجم الموجود على جهازك:

```powershell
./build.ps1 -InnoCompiler 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

ينتج `artifacts/Setup-Small.exe`، ويحتاج جهاز العميل .NET Framework 4.8 مثبتًا مسبقًا.

لإنشاء `artifacts/Setup.exe` الكامل للعمل دون إنترنت، ضع مثبت Microsoft الأصلي الموقّع `ndp48-x86-x64-allos-enu.exe` داخل `.tools/`، ثم أضف `-BundleFramework` للأمر السابق. ملف Microsoft ومترجم Inno غير مضمنين هنا. يتحقق سكربت البناء من توقيع Microsoft قبل التجميع.

## الاختبارات

الفحوص التالية تُنشئ قواعد بيانات تجريبية مستقلة، ولا تحتاج قاعدة البيانات القديمة:

```powershell
& ./tests/OperationalChecks/bin/Release/net48/OperationalChecks.exe qa-output
& ./tests/PerformanceChecks/bin/Release/net48/PerformanceChecks.exe performance-output
& ./tests/PerformanceChecks/bin/Release/net48/PerformanceChecks.exe performance-output --reload-bench
```

استخدم مجلد نتائج جديدًا عند إعادة اختبار الأداء الكامل. اختبار الأداء يولد 1000 صنف و10000 أوردر صناعي. فحوص التشغيل تشمل الحسابات والصلاحيات والحفظ والمخزون والنسخ الاحتياطية والنوافذ. خيار `--print-emulator` اختياري ويرسل إيصال اختبار حقيقيًا للمحاكي؛ لا تستخدمه إلا إذا أردت ذلك.

الأرقام المحلية لا تغني عن اختبار الطابعة الفعلية ونظام Windows ودقة العرض لدى العميل. لم يتم اعتماد أجهزة Windows 7/10 أو DPI مختلف أو وردية طويلة في هذه الجولة. البرنامج غير موقّع رقميًا.

## رفع الملفات

ارفع محتويات هذا المجلد كمستودع. `.gitignore` يستبعد قواعد البيانات وبيانات التشغيل والمخرجات والأدوات والملفات المؤقتة. ارفع ملفات التثبيت في **GitHub Releases** بشكل منفصل؛ لا تضفها إلى ملفات السورس.

دليل التشغيل: [USER-GUIDE.txt](USER-GUIDE.txt). المكونات الخارجية وحقوق الشعار: [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt). لا يتضمن المشروع ترخيصًا جديدًا يمنح حقوقًا إضافية في الشعار أو الكود.
