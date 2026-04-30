# <Special Azeroth Service>

Outil de gestion et d'organisation des fenêtres World of Warcraft pour le multiboxing.
Permet de positionner automatiquement vos fenêtres WoW sur un ou plusieurs écrans avec différents modes de disposition.

---

## Installation

1. Télécharger le dernier `.zip` depuis la page [Releases](https://github.com/yenemillas-Cyco/SpecialAzerothService/releases)
2. Extraire l'archive
3. Lancer `SpecialAzerothService.exe`

> Aucune installation requise — l'application est autonome (self-contained).

---

## Fonctionnalités

### Liste des fenêtres

L'application détecte automatiquement toutes les fenêtres WoW ouvertes.

| Action | Description |
|---|---|
| **Sélection** | Cocher/décocher une fenêtre pour l'inclure dans la disposition |
| **Renommer** | Cliquer sur le nom dans la liste ou dans l'aperçu pour le modifier. Le nom est conservé après un rafraîchissement |
| **Leader** | Désigner une fenêtre comme leader (fenêtre principale) par écran. En mode Principal, c'est elle qui occupe la zone dominante |
| **Plein écran** | Basculer une fenêtre en plein écran. Un second clic restaure sa position précédente |
| **Écran** | Choisir sur quel moniteur placer chaque fenêtre via le menu déroulant |
| **Réorganiser** | Glisser-déposer (drag & drop) pour changer l'ordre des fenêtres dans la liste |
| **Rafraîchir** | Bouton ⟳ pour re-détecter les fenêtres (noms personnalisés et états conservés) |

### Modes de disposition

Chaque écran peut être configuré indépendamment avec son propre mode.

#### Mode Principal

La fenêtre **Leader** occupe la zone dominante, les autres se répartissent autour.

- **Taille** : Grand (75%), Moyen (67%), Petit (50%) — proportion de l'écran occupée par le leader
- **Position** : ↖ ↗ ↘ ↙ — coin de l'écran où placer le leader
- **Zones** :
  - **Latéral** : les secondaires s'empilent verticalement à côté du leader
  - **Bandeau** : les secondaires s'alignent horizontalement sous/sur le leader
  - **Les deux** : disposition mixte avec latéral + bandeau (grille avancée)

#### Mode Fractionné

Toutes les fenêtres ont la même taille, réparties en grille.

- **Orientation** :
  - **▥ Côte à côte** (Horizontal) : privilégie les colonnes (ex. 6 fenêtres → 3×2)
  - **▤ Empilé** (Vertical) : privilégie les lignes (ex. 6 fenêtres → 2×3)

### Multi-écran

- Détection automatique de tous les moniteurs connectés
- Chaque écran dispose de son propre panneau de configuration avec :
  - Choix du mode (Principal / Fractionné)
  - Options spécifiques au mode
  - Aperçu en temps réel de la disposition
- Les panneaux sont affichés côte à côte avec défilement horizontal si nécessaire
- Chaque fenêtre peut être assignée à un écran différent via le menu déroulant

### Aperçu en temps réel

Chaque panneau de configuration d'écran inclut un aperçu miniature montrant :
- Le contour de l'écran
- La position et taille de chaque fenêtre assignée
- Le nom personnalisé de chaque fenêtre
- Mise à jour automatique à chaque changement d'option

---

## Appliquer la disposition

Cliquer sur **⚔ Appliquer** pour déplacer et redimensionner toutes les fenêtres sélectionnées selon la configuration définie.

---

## Raccourcis

| Raccourci | Action |
|---|---|
| Clic sur un nom (liste) | Renommer la fenêtre |
| Clic sur un nom (aperçu) | Renommer la fenêtre |
| Drag & drop (liste) | Réorganiser l'ordre |

---

## Configuration technique

- **Framework** : .NET 10 / WPF
- **Architecture** : MVVM (CommunityToolkit.Mvvm)
- **API Windows** : Win32 P/Invoke (SetWindowPos, EnumWindows, EnumDisplayMonitors)
- **Cible** : Windows 10/11 x64

---

## Développement

```bash
# Cloner
git clone https://github.com/yenemillas-Cyco/SpecialAzerothService.git

# Build
dotnet build WindowsOrganiserApp/WindowsOrganiserApp.csproj

# Publier (self-contained)
dotnet publish WindowsOrganiserApp/WindowsOrganiserApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## Auteur

**Cyco** (ancien stagiaire) — yenemillas@gmail.com

## Remerciements

- **Opti** — Bêta-testeur officiel, cobaye volontaire
- **Eloi** — Grand Maître de la guilde, dictateur d'Azeroth Service
