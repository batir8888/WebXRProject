# WebXR AR: объект в центре поля зрения + drag & scale (Unity 6 + WebXR Export + XR Interaction Toolkit)

AR-сцена на Unity **6000.6.8f1** (URP), собранная в WebGL с пакетом
[De-Panther/unity-webxr-export](https://github.com/De-Panther/unity-webxr-export) **0.25.0 + исправления для Unity 6.5+ из master (коммит `1f7a06c`)**.
При старте куб появляется в центре поля зрения пользователя (viewer-space). Это происходит и в обычном браузере, и после входа в AR-сессию.

**Демо (HTTPS, GitHub Pages):** https://batir8888.github.io/WebXRProject/

## Задание 7: перетаскивание и масштабирование (XR Interaction Toolkit)

Куб можно взять, перетащить и масштабировать. Работает и пальцем на телефоне в AR-сессии, и мышью в обычном браузере, и контроллерами (Quest, passthrough AR / VR).

| Требование | Реализация |
|---|---|
| XR Interaction Toolkit | `com.unity.xr.interaction.toolkit` **3.7.0**. На кубе стоят `XRGrabInteractable` (Instantaneous, kinematic `Rigidbody`, `BoxCollider`, dynamic attach), `XRGeneralGrabTransformer` и собственный `PinchScaleGrabTransformer`. Взаимодействие идёт через `XRRayInteractor`, в сцене есть `XRInteractionManager`. |
| webxr-xrit-bridge | Пакет [`com.someone-s.webxr-xrit-bridge`](https://github.com/someone-s/webxr-xri-toolkit-bridge) подключён как **embedded-пакет** `Packages/com.someone-s.webxr-xrit-bridge` (форк 0.1.2, Apache-2.0). Оригинал 2023 года **не компилируется** с WebXR Export 0.25: изменилась сигнатура `OnHeadsetUpdate`. Его `WebXRHeadsetAdapter` при входе в AR выключает `Camera.main` и ломает камеру WebXR. Что исправлено, описано в [CHANGELOG форка](Packages/com.someone-s.webxr-xrit-bridge/CHANGELOG.md). В сцене стоит префаб моста `WebXR XR Interaction Toolkit Bridge` с `WebXRControllerAdapter`. Он создаёт Input System-устройства `<XRController>{LeftHand/RightHand}`. Из них `TrackedPoseDriver` и `XRRayInteractor` контроллеров берут позу и кнопки (trigger / grip = select). Встроенный `WebXRInputSystem` из WebXR Interactions отключён (`AutoLoadWebXRInputSystem = false`), иначе появилась бы вторая пара устройств LeftHand/RightHand. |
| Перемещение | Ray-интеракция. `useForceGrab = false`, поэтому объект едет за лучом на той дистанции, где его взяли, без притягивания к руке. Вращение не отслеживается, куб остаётся вертикальным. |
| Масштабирование | **Телефон:** щипок двумя пальцами (`PinchScaleGrabTransformer`, множитель = отношение расстояний между пальцами). Пока идёт щипок, луч замораживается, чтобы куб не уезжал. **Десктоп:** колесо мыши при зажатой ЛКМ. **Контроллеры:** взять куб двумя контроллерами и развести их (`allowTwoHandedScaling`). Масштаб ограничен 5–100 см (0,25×–5× для двух рук). |
| Визуальный отклик | `InteractionFeedback` слушает события XRIT. Наведение (hover): куб светлеет, появляется белый контур (inverted hull). Захват (select): куб становится оранжевым, появляется пульсирующий жёлтый контур и короткая вспышка, вращение `Spin` ставится на паузу. Луч контроллера подсвечивается жёлтым, когда указывает на куб. |
| WebGL + WebXR AR | Касания в AR на телефоне — это WebXR input source с `targetRayMode = "screen"` и `handedness = "none"`. WebXR Export превращает их в обычные touch-события canvas, поэтому контроллеров на телефоне нет. Их место занимает `ScreenPointerRayDriver`: каждый кадр он ставит отдельный `XRRayInteractor` на луч «камера → палец» и вручную подаёт ему select (`XRInputButtonReader`, режим ManualValue). Состояние пишется напрямую в `manualPerformed`, а не через `QueueManualState`: тот применяет значение только со следующего кадра, и при вызове каждый кадр до чтения XRIT значение так и не применялось бы. В AR луч строится через матрицу проекции WebXR-вида (`WebXRManager.OnHeadsetUpdate`), а не через FOV камеры. Скрипт выполняется до `XRInteractionManager` (`DefaultExecutionOrder(-200)`), так что XRIT работает с лучом текущего кадра. |

Почему не штатные `TouchscreenGestureInputController` / `ScreenSpace*Input` из XRIT: они требуют AR Foundation, а в WebGL AR Foundation нет.

### Чек-лист ручного теста (задание 7)

1. **Десктоп, обычный режим:** наведите мышь на куб — он светлеет и получает белый контур. Зажмите ЛКМ — куб становится оранжевым и перестаёт вращаться. Тащите — куб едет за курсором. Колесо при зажатой ЛКМ меняет размер. Отпустите — куб снова синий и вращается.
2. **Android Chrome, AR:** нажмите **AR**. Коснитесь куба — он оранжевый с жёлтым пульсирующим контуром. Ведите пальцем — куб перемещается в пространстве. Поставьте второй палец и разведите/сведите — куб масштабируется.
3. **Quest Browser, AR (passthrough) или VR:** появятся лучи из контроллеров. Наведите луч — луч и куб подсвечиваются. Trigger или grip — взять и перенести. Возьмите двумя контроллерами и разведите — масштаб.

Проверено на локальной WebGL-сборке в Chrome, обычный режим (мышь, а также синтетические touch-события):
- наведение;
- захват;
- перетаскивание;
- масштаб колесом (0,2 → 0,322);
- щипок двумя касаниями (0,322 → 0,064);
- отпускание.

Формула луча для AR (unproject через матрицу проекции) сверена с `Camera.ScreenPointToRay` в редакторе, расхождение 0°. Пункты 2–3 требуют устройства.

## Что сделано (задание 6)

| Требование | Реализация |
|---|---|
| Подключён unity-webxr-export | `com.de-panther.webxr` и `com.de-panther.webxr-interactions` подключены как git-пакеты из репозитория De-Panther, закреплённые на коммите `1f7a06c` (`Packages/manifest.json`). В XR Plug-in Management для WebGL назначен loader `WebXR.WebXRLoader` и включён Initialize on Startup (`Assets/XR/`). |
| Камера WebXR и WebXRController | В сцене `Assets/Scenes/ARScene.unity` стоит префаб `WebXRCameraSet` из WebXR Interactions: Main Camera с `WebXRCameraSettings` и `handL`/`handR` с `WebXRController`. Риг находится в начале координат пространства WebXRManager. |
| Объект в центре экрана при старте | Объект `XRSpaceRoot` со скриптом `ViewerSpacePlacer` ставит префаб `ARCube` (куб 20 см) в точку `camera.position + camera.forward × 0.8 м`. Это делается в `Start` и повторно через 3 кадра после `WebXRManager.OnXRChange` (вход в AR/VR и выход), когда поза головы уже получена. |
| Шаблон WebXRTemplate | WebGL-шаблон `WebXRFullView2020` скопирован из пакета в `Assets/WebGLTemplates/`. В нём есть кнопки AR и VR. |
| Настройки WebGL | Compression Format **Disabled**, Decompression Fallback **off**, Multithreading (threadsSupport) **off**. Графика только WebGL 2 (без WebGPU), цветовое пространство Linear. |
| Упрощение отладки | На камере отключены post-processing, HDR и MSAA. Нет лишних плагинов и эффектов. В задании 6 у куба не было физики. В задании 7 вернули `BoxCollider` и kinematic `Rigidbody`: без них не работают raycast-ы XRIT и `XRGrabInteractable`. |

Настройки AR (`Assets/XR/Settings/WebXRSettings.asset`): обязательное reference space — `local`, опциональная фича — `hit-test`.
`local` выбран вместо `local-floor`, потому что его поддерживает больше мобильных устройств. Начало координат тогда совпадает с положением головы в момент старта сессии.

## Почему не OpenUPM 0.25.0

В релизе 0.25.0 на OpenUPM (май 2026) `webxr.jspre` обращается к глобальному объекту Emscripten `Browser`. В Unity 6.5+ его нет: main loop перенесён в `MainLoop`.
Из-за этого сборка падает при старте с ошибкой `ReferenceError: Browser is not defined`.
Исправления (PR #446 и #447) уже есть в master, но в релиз пока не вошли. Поэтому пакеты взяты из git на коммите `1f7a06c`.
Когда выйдет 0.26+, можно вернуть scoped registry OpenUPM.

## Как пересобрать

Вся настройка проекта воспроизводима из кода: `Assets/Editor/ProjectSetup.cs`, меню **WebXR Demo**.

```bash
unity run . --editor-version 6000.6.8f1 -- -buildTarget WebGL -executeMethod ProjectSetup.SetupAll
unity run . --editor-version 6000.6.8f1 -- -buildTarget WebGL -executeMethod ProjectSetup.BuildWebGL
./deploy-gh-pages.sh      # публикует Builds/WebGL в ветку gh-pages
```

Локальный HTTPS-сервер для теста по Wi-Fi (WebXR работает только в secure context):

```bash
npx http-server Builds/WebGL -S -C cert.pem -K key.pem -p 8443
```

Сертификат можно выпустить через `mkcert <IP-компьютера>`. Есть и другой вариант: `chrome://inspect` → Port forwarding `8080 → localhost:8080` по USB. Тогда страница на телефоне открывается как `http://localhost:8080` и тоже считается secure context.

## Размер сборки

| Файл | Без сжатия | gzip -9 |
|---|---:|---:|
| `WebGL.wasm` | 25,5 МБ | 8,3 МБ |
| `WebGL.data` | 7,8 МБ | 2,6 МБ |
| `WebGL.framework.js` | 0,40 МБ | 0,09 МБ |
| `WebGL.loader.js` | 0,03 МБ | 0,01 МБ |
| **Итого** | **33,7 МБ** | **≈11,0 МБ** |

* Основной объём занимает код движка и URP в `.wasm`. Стриппинг уже включён: Managed Stripping = High, Strip Engine Code, IL2CPP Optimize Size.
* В `.data` самые крупные элементы: логотип Splash Screen (2,7 МБ) и шейдеры URP (около 0,6 МБ). Пользовательские ассеты занимают всего 4,3 МБ.
* Сжатие отключено по требованию (fallback выключен). На хостинге без `Content-Encoding` это единственный рабочий вариант. GitHub Pages не умеет отдавать заранее сжатые `.br` и `.gz` с нужным заголовком.
* Как уменьшить сборку: Brotli при правильной настройке сервера (около 7–8 МБ), отключение Splash Screen (нужна платная лицензия Unity), Built-in RP вместо URP (меньше wasm, но WebXR Export 0.25 рекомендует URP), удаление неиспользуемых модулей (physics, audio, UI).

## Совместимость

| Платформа и браузер | Обычный режим (3D-страница) | immersive-ar | Примечание |
|---|---|---|---|
| Android + Chrome (ARCore / Google Play Services for AR) | ✅ | ✅ | основная целевая платформа |
| Android + Samsung Internet | ✅ | ✅ (новые версии) | |
| Android + Firefox | ✅ | ❌ | WebXR не поддерживается |
| Meta Quest Browser | ✅ | ✅ (passthrough) | также доступен VR |
| iOS / iPadOS Safari, Chrome на iOS | ✅ | ❌ | в WebKit на iPhone нет WebXR; кнопка AR будет неактивна |
| Apple Vision Pro Safari | ✅ | ❌ (только immersive-vr) | |
| Десктоп Chrome / Edge / Firefox | ✅ | ❌ | кнопка AR неактивна; для отладки подходит расширение Immersive Web Emulator |

Кнопка **AR** в шаблоне становится активной, только если `navigator.xr.isSessionSupported('immersive-ar')` вернул `true`.

## Чек-лист ручного теста на устройстве

1. Открыть демо по HTTPS в Chrome на Android-устройстве с ARCore.
2. Дождаться загрузки (≈34 МБ). Синий вращающийся куб должен быть по центру экрана.
3. Нажать **AR** и разрешить доступ к камере. Должна появиться камера-passthrough, а куб оказаться в центре поля зрения примерно в 0,8 м.
4. Отойти и обойти куб: он остаётся на месте в мировом пространстве.
5. Выйти из AR (кнопка «назад»). Куб снова появится в центре экрана.
