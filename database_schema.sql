PGDMP  #                     }            mtcg    17.2    17.2     �           0    0    ENCODING    ENCODING        SET client_encoding = 'UTF8';
                           false            �           0    0 
   STDSTRINGS 
   STDSTRINGS     (   SET standard_conforming_strings = 'on';
                           false            �           0    0 
   SEARCHPATH 
   SEARCHPATH     8   SELECT pg_catalog.set_config('search_path', '', false);
                           false            �           1262    24576    mtcg    DATABASE     x   CREATE DATABASE mtcg WITH TEMPLATE = template0 ENCODING = 'UTF8' LOCALE_PROVIDER = libc LOCALE = 'German_Germany.1252';
    DROP DATABASE mtcg;
                     postgres    false                        3079    32981 	   uuid-ossp 	   EXTENSION     ?   CREATE EXTENSION IF NOT EXISTS "uuid-ossp" WITH SCHEMA public;
    DROP EXTENSION "uuid-ossp";
                        false            �           0    0    EXTENSION "uuid-ossp"    COMMENT     W   COMMENT ON EXTENSION "uuid-ossp" IS 'generate universally unique identifiers (UUIDs)';
                             false    2            �            1259    32939    cards    TABLE     �   CREATE TABLE public.cards (
    id uuid NOT NULL,
    name character varying(255) NOT NULL,
    damage double precision NOT NULL,
    package_id uuid,
    user_id integer,
    "Element" character varying(50),
    "Type" character varying(50)
);
    DROP TABLE public.cards;
       public         heap r       postgres    false            �            1259    32944    package_cards    TABLE     b   CREATE TABLE public.package_cards (
    package_id integer NOT NULL,
    card_id uuid NOT NULL
);
 !   DROP TABLE public.package_cards;
       public         heap r       postgres    false            �            1259    32998    packages    TABLE     ?   CREATE TABLE public.packages (
    package_id uuid NOT NULL
);
    DROP TABLE public.packages;
       public         heap r       postgres    false            �            1259    33003 	   user_deck    TABLE     [   CREATE TABLE public.user_deck (
    user_id integer NOT NULL,
    card_id uuid NOT NULL
);
    DROP TABLE public.user_deck;
       public         heap r       postgres    false            �            1259    32918    users    TABLE     �  CREATE TABLE public.users (
    username character varying(50) NOT NULL,
    password character varying(100) NOT NULL,
    image character varying(100),
    token character varying(100),
    coins integer DEFAULT 20,
    wins integer DEFAULT 0,
    losses integer DEFAULT 0,
    bio character varying(100),
    id integer NOT NULL,
    elo integer DEFAULT 100,
    ingame_name character varying(50)
);
    DROP TABLE public.users;
       public         heap r       postgres    false            �            1259    32921    users_id_seq    SEQUENCE     �   CREATE SEQUENCE public.users_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;
 #   DROP SEQUENCE public.users_id_seq;
       public               postgres    false    218            �           0    0    users_id_seq    SEQUENCE OWNED BY     =   ALTER SEQUENCE public.users_id_seq OWNED BY public.users.id;
          public               postgres    false    219            ?           2604    32922    users id    DEFAULT     d   ALTER TABLE ONLY public.users ALTER COLUMN id SET DEFAULT nextval('public.users_id_seq'::regclass);
 7   ALTER TABLE public.users ALTER COLUMN id DROP DEFAULT;
       public               postgres    false    219    218            �          0    32939    cards 
   TABLE DATA           Y   COPY public.cards (id, name, damage, package_id, user_id, "Element", "Type") FROM stdin;
    public               postgres    false    220   >       �          0    32944    package_cards 
   TABLE DATA           <   COPY public.package_cards (package_id, card_id) FROM stdin;
    public               postgres    false    221   %       �          0    32998    packages 
   TABLE DATA           .   COPY public.packages (package_id) FROM stdin;
    public               postgres    false    222   (%       �          0    33003 	   user_deck 
   TABLE DATA           5   COPY public.user_deck (user_id, card_id) FROM stdin;
    public               postgres    false    223   &       �          0    32918    users 
   TABLE DATA           q   COPY public.users (username, password, image, token, coins, wins, losses, bio, id, elo, ingame_name) FROM stdin;
    public               postgres    false    218   )&       �           0    0    users_id_seq    SEQUENCE SET     <   SELECT pg_catalog.setval('public.users_id_seq', 221, true);
          public               postgres    false    219            D           2606    32943    cards cards_pkey 
   CONSTRAINT     N   ALTER TABLE ONLY public.cards
    ADD CONSTRAINT cards_pkey PRIMARY KEY (id);
 :   ALTER TABLE ONLY public.cards DROP CONSTRAINT cards_pkey;
       public                 postgres    false    220            F           2606    32948     package_cards package_cards_pkey 
   CONSTRAINT     o   ALTER TABLE ONLY public.package_cards
    ADD CONSTRAINT package_cards_pkey PRIMARY KEY (package_id, card_id);
 J   ALTER TABLE ONLY public.package_cards DROP CONSTRAINT package_cards_pkey;
       public                 postgres    false    221    221            H           2606    33002    packages packages_pkey 
   CONSTRAINT     \   ALTER TABLE ONLY public.packages
    ADD CONSTRAINT packages_pkey PRIMARY KEY (package_id);
 @   ALTER TABLE ONLY public.packages DROP CONSTRAINT packages_pkey;
       public                 postgres    false    222            J           2606    33007    user_deck user_deck_pkey 
   CONSTRAINT     d   ALTER TABLE ONLY public.user_deck
    ADD CONSTRAINT user_deck_pkey PRIMARY KEY (user_id, card_id);
 B   ALTER TABLE ONLY public.user_deck DROP CONSTRAINT user_deck_pkey;
       public                 postgres    false    223    223            B           2606    32927    users users_pkey 
   CONSTRAINT     N   ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (id);
 :   ALTER TABLE ONLY public.users DROP CONSTRAINT users_pkey;
       public                 postgres    false    218            K           2606    32954 (   package_cards package_cards_card_id_fkey    FK CONSTRAINT     �   ALTER TABLE ONLY public.package_cards
    ADD CONSTRAINT package_cards_card_id_fkey FOREIGN KEY (card_id) REFERENCES public.cards(id) ON DELETE CASCADE;
 R   ALTER TABLE ONLY public.package_cards DROP CONSTRAINT package_cards_card_id_fkey;
       public               postgres    false    4676    220    221            L           2606    33013     user_deck user_deck_card_id_fkey    FK CONSTRAINT     �   ALTER TABLE ONLY public.user_deck
    ADD CONSTRAINT user_deck_card_id_fkey FOREIGN KEY (card_id) REFERENCES public.cards(id) ON DELETE CASCADE;
 J   ALTER TABLE ONLY public.user_deck DROP CONSTRAINT user_deck_card_id_fkey;
       public               postgres    false    223    4676    220            M           2606    33008     user_deck user_deck_user_id_fkey    FK CONSTRAINT     �   ALTER TABLE ONLY public.user_deck
    ADD CONSTRAINT user_deck_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE;
 J   ALTER TABLE ONLY public.user_deck DROP CONSTRAINT user_deck_user_id_fkey;
       public               postgres    false    4674    223    218            �   �  x��W=o�����/
$���>E�H�4א��Y�����Qv}~���.CI�Ù�^e��3E6Kly&]\S�LE��:���e��������Wr��R��1������%�IyP�����O�"Zb�{ɫ��]�X�z{���}���]Gom�䔋LM>q��e�p��~���{��<�:C԰�����mEr�+�fUj��5������:)K+	g��UN�T��I�R�u{W�u����'���j�4��d:G�1���6��(�`���H�Y[�^sҒ+��ئOї�MuF"$I�x$�4{ӱ7�C�]�t�!f��^%:2�-�_B�2s�����H�NT�AN/�Z	�Z�������{-_os8����	���#���Gϋq	~�^�z}��M<XЉ��Q뚭6��<FUJdIZw(�HcGK�x�2�O��R�#v�J��Ȁ���2,�>e����6:͡s�x+�+���P�NX��;����T0�8��!,����yi���_s�J-'HOLZ۲4H�V�>�����Q{�M��"��SŘQ�l�7��Ͱ�u���Q_uqN�pi�c$�U�D�<�[��(b��.���@g�f�J����ɞ��2�ֶ���T��z�f0^�?}|���e��#�/��}��9��f^{���|z�Z��@��Y�鲊K��^�����^'f�P�z�Y�^���3U��j�w�>Gn%U��]����l1�v��@��uyQQ��)x�v��jem�<�)�my/��b½8������ẓ{�E��t�u�{����W�z���vPH 3g�uy/C�f���#��Dp��l�Yg��_o3x.2�S��dؔ<0�y6i��#�o�w�S����q�� ��+�c��o)V������~� N���s�Թ���_F���2c�z�;�4J��L1���(���z�4������iBV���߆�%$�zx{���x��# 4�Y��7����k�OfX�a��b���E`?L8*L�1ٽ����n���x���'C�^�A.0p�P/���0���|����9��h1� ��j}?��������G{�ۆ�#ug�\ؓy4�Vwدg��m�Ա�F�F���s��=T�QЀ}>����8�����-`؏�2�F���ކ��7s]͗�;/XF�N�мB��O��,��m���w�:R�A���䥵�s�O�Ņ*���0�Ԍa�`.��[n�����J�s�u�1;r@����*ߊ�����7�����e){-Zo���Ȭ�*;^/� !iiκ2(�����M`�a�چ&A3�����-J�I���l��r���``o5ؒ��b
���Ԯs,�c�kB�0��h'�roڒ�!_G�d�ۉn���y@�-��<����`�.s�@��Dyp�������Ok�ۜ�����O�>�jER�      �      x������ � �      �   �   x����E!C���9((��l��������$�����q"��nM�;���<G�~N{�S��>U4�����5�]��k��<ɬ�j�>��=�W��%�����R��Љ3G,>�ҁq3Ķ��n��oO/�㘈�
���xÔ	6�e�������{g#
�d�ռ�m����|�������0�+-r\޻K��_����/�q�v���1��BU      �      x������ � �      �     x�m��O�0��s�+<p�֐�7's�m�6@v)�m
�Q���;"$̻��%�O�D���X��g���v��<.�B������4:݇�L���c�v�߹�X��UFJ'[��e	���^0F Y�j�3YƕL��ƴ�s�Ͼm�L�zCۮ�hFvV���k���O�7/���$�+F� �S�_QɛC!ڬ����~,�,w�F�<�f1�0�$'q�F[�<1N����N�fA"zAT��ꊻ@����J{����^C�0~ �Qj�     