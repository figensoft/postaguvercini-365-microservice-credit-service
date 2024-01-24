# Kurulum için Bilinmesi Gerekenler

- `.env.template` dosyasını kopyalayıp, ismini `.env` yapınız, .env.template dosyasını silmeyiniz, (`.gitignore` dosyanızda `**/.env` satırının olması gerekiyor, yoksa ekleyiniz, eklemezseniz şifreler github reposuna gider), kurulum yapacağınız ortama göre (development, production, test vb.) .env dosyasının içerisindeki bilgileri uygulamanıza göre düzenleyiniz

- `.env` dosyasındaki `COMPOSE_PROJECT_NAME` ve `PROJECT` ayrı bilgilerdir, PROJECT projemizin ismi ve imageların taglerinde kullanılıyor, 
  COMPOSE_PROJECT_NAME ise docker da gruplama ve container isimlerinin ayrışması için gerekli, localimizde bu 2 bilgi aynı olmakla beraber, kurulum yaptığımız prod ortamlarına göre
  COMPOSE_PROJECT_NAME farklı olabilir, örneğin postaguvercini-365-xxxx gibi

- `build.sh` dosyası ve diğer .sh dosyalarındaki versiyon ve image, container isimlerinin doğruluğundan emin olunuz, 
  `appsettings.json` (backend), `jsf.config.json` (frontend) dosyalarındaki versiyonlarla aynı olması gerekir

- `docker-compose.yml` dosyanızı kontrol ediniz, bilgiler, portlar ve volume pathlerin vb. doğruluğundan emin olunuz, projeyi localdeki kodla çalıştırmak istiyorsanız, 
  yorum satırındaki volume u kendi bilgisayarınızdaki pathlere göre düzenleyiniz. compose daki `environment` bölümünden configuration ayarlamaları yapabilirsiniz

- Sadece uygulamanın docker image ını build etmek istiyorsanız, `build.sh` scriptini çalıştırabilirsiniz

- Yüklemek istediğiniz uygulamanın versiyonunun image ı zaten docker da yüklü ise sadece `deploy.sh` dosyasını çalıştırarak deploy edebilirsiniz

- Hem image ı oluşturup, hem de deploy etmek istiyorsanız `build-and-deploy.sh` dosyasını çalıştırınız

- Hali hazırda kurulu olan bir container image ını export etmek için `export.sh` dosyasını çalıştırınız