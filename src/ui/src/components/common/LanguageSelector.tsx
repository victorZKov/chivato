import { useTranslation } from "react-i18next";
import "./LanguageSelector.css";

const languages = [
  { code: "en", name: "English", flag: "🇬🇧" },
  { code: "es", name: "Español", flag: "🇪🇸" },
];

export function LanguageSelector() {
  const { i18n } = useTranslation();

  const currentLang = languages.find((l) => l.code === i18n.language) || languages[0];

  const handleChange = (langCode: string) => {
    i18n.changeLanguage(langCode);
  };

  return (
    <div className="language-selector">
      <button
        className="btn btn-ghost language-toggle"
        title={currentLang.name}
        onClick={() => {
          const currentIndex = languages.findIndex((l) => l.code === i18n.language);
          const nextIndex = (currentIndex + 1) % languages.length;
          handleChange(languages[nextIndex].code);
        }}
      >
        <span className="language-flag">{currentLang.flag}</span>
        <span className="language-code">{currentLang.code.toUpperCase()}</span>
      </button>
    </div>
  );
}
